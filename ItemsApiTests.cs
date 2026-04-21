using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Services;
using Xunit;

namespace Pr1.MinWebService.Tests;

/// <summary>
/// Автотесты для проверки конвейера обработки запросов.
/// </summary>
public class ItemsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ItemsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Тест 1: Создание -> Получение по ID -> Получение списка.
    /// Проверяет полный жизненный цикл элемента.
    /// </summary>
    [Fact]
    public async Task Create_GetById_GetAll_ShouldWork()
    {
        // Arrange
        var newItem = new { Name = "Автотест-книга", Price = 999m };

        // Act: Создание
        var createResponse = await _client.PostAsJsonAsync("/api/items", newItem);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdItem = await createResponse.Content.ReadFromJsonAsync<Item>();
        createdItem.Should().NotBeNull();
        createdItem!.Name.Should().Be("Автотест-книга");
        createdItem.Price.Should().Be(999m);
        
        // Проверяем заголовок Location
        createResponse.Headers.Location.Should().NotBeNull();
        createResponse.Headers.Location!.ToString().Should().Contain(createdItem.Id.ToString());

        // Act: Получение по ID
        var getResponse = await _client.GetAsync($"/api/items/{createdItem.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fetchedItem = await getResponse.Content.ReadFromJsonAsync<Item>();
        fetchedItem.Should().BeEquivalentTo(createdItem);

        // Act: Получение всех
        var listResponse = await _client.GetAsync("/api/items");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var items = await listResponse.Content.ReadFromJsonAsync<List<Item>>();
        items.Should().ContainSingle(i => i.Id == createdItem.Id);
    }

    /// <summary>
    /// Тест 2: Запрос несуществующего ID должен вернуть 404 и единый формат ошибки.
    /// </summary>
    [Fact]
    public async Task GetById_NotFound_ReturnsErrorResponse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/items/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("not_found");
        error.Message.Should().Be("Элемент не найден");
        error.RequestId.Should().NotBeNullOrEmpty();
        
        // Проверяем, что response content type - JSON
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    /// <summary>
    /// Тест 3: Создание с пустым именем → ошибка валидации.
    /// </summary>
    [Fact]
    public async Task Create_EmptyName_ReturnsValidationError()
    {
        // Arrange
        var invalidItem = new { Name = "", Price = 100m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", invalidItem);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("validation");
        error.Message.Should().Contain("name не должно быть пустым");
        error.RequestId.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Тест 4: Создание с отрицательной ценой → ошибка валидации.
    /// </summary>
    [Fact]
    public async Task Create_NegativePrice_ReturnsValidationError()
    {
        // Arrange
        var invalidItem = new { Name = "Книга", Price = -50m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", invalidItem);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("validation");
        error.Message.Should().Contain("price не может быть отрицательным");
    }

    /// <summary>
    /// Тест 5: Проверка проброса RequestId через заголовок.
    /// </summary>
    [Fact]
    public async Task RequestId_FromHeader_IsPropagatedToResponse()
    {
        // Arrange
        var customRequestId = "test-id-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.Add("X-Request-Id", customRequestId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Contains("X-Request-Id").Should().BeTrue();
        var returnedId = response.Headers.GetValues("X-Request-Id").First();
        returnedId.Should().Be(customRequestId);
    }

    /// <summary>
    /// Тест 6: При отсутствии заголовка X-Request-Id генерируется новый ID.
    /// </summary>
    [Fact]
    public async Task RequestId_WhenNoHeader_GeneratesNewId()
    {
        // Act
        var response = await _client.GetAsync("/api/items");

        // Assert
        response.Headers.Contains("X-Request-Id").Should().BeTrue();
        var generatedId = response.Headers.GetValues("X-Request-Id").First();
        generatedId.Should().NotBeNullOrEmpty();
        generatedId.Should().MatchRegex("^[a-f0-9]{32}$"); // GUID без дефисов
    }

    /// <summary>
    /// Тест 7: Несколько последовательных запросов имеют разные ID.
    /// </summary>
    [Fact]
    public async Task RequestId_IsDifferentForDifferentRequests()
    {
        // Act
        var response1 = await _client.GetAsync("/api/items");
        var response2 = await _client.GetAsync("/api/items");

        // Assert
        var id1 = response1.Headers.GetValues("X-Request-Id").First();
        var id2 = response2.Headers.GetValues("X-Request-Id").First();
        
        id1.Should().NotBe(id2);
    }

    /// <summary>
    /// Тест 8: POST возвращает Location с корректным URL.
    /// </summary>
    [Fact]
    public async Task Create_ReturnsCorrectLocationHeader()
    {
        // Arrange
        var newItem = new { Name = "Location-тест", Price = 777m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", newItem);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("/api/items/");
        
        // Проверяем, что по этому URL можно получить элемент
        var getResponse = await _client.GetAsync(location);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Тест 9: Репозиторий действительно хранит данные между запросами.
    /// </summary>
    [Fact]
    public async Task Repository_PersistsDataBetweenRequests()
    {
        // Arrange: создаём элемент
        var newItem = new { Name = "Постоянный элемент", Price = 123m };
        var createResponse = await _client.PostAsJsonAsync("/api/items", newItem);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>();
        
        // Act: делаем новый клиент (новый экземпляр, но тот же сервер)
        using var newClient = _factory.CreateClient();
        var listResponse = await newClient.GetAsync("/api/items");
        
        // Assert
        var items = await listResponse.Content.ReadFromJsonAsync<List<Item>>();
        items.Should().Contain(i => i.Id == created!.Id);
    }
}   