Запуск системы
Dotnet run

Чек-лист
№ Сценарий Команда
1 Создать элемент Invoke-RestMethod -Uri "http://localhost:54255/api/items" -Method POST -ContentType "application/json" -Body '{"Name":"World and peace","Price":1200}'
2 Получить все элементы curl.exe http://localhost:54255/api/items
3 Получить по ID curl.exe "http://localhost:54255/api/items/ID"
4 Ошибка 404 curl.exe "http://localhost:54255/api/items/11111111-1111-1111-1111-111111111111"
5 Ошибка пустое имя Invoke-RestMethod -Uri "http://localhost:54255/api/items" -Method POST -ContentType "application/json" -Body '{"Name":"","Price":100}'
6 Ошибка: отриц. цена Invoke-RestMethod -Uri "http://localhost:54255/api/items" -Method POST -ContentType "application/json" -Body '{"Name":"Война и мир","Price":-1200}'
7 Проверка RequestId curl.exe -H "X-Request-Id: test-123" http://localhost:54255/api/items -v
