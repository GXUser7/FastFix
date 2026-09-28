#!/bin/sh
# Пересоздание БД servicedesk с демо-данными (локальный MySQL, строка подключения из appsettings.Local.json)
cd "$(dirname "$0")/.."
export MYSQL_PWD=$(python -c "import json;print(json.load(open('src/ServiceDesk.Api/appsettings.Local.json',encoding='utf-8'))['ConnectionStrings']['Default'].split('Password=')[1].split(';')[0])")
M="/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe"
"$M" -u servicedesk --default-character-set=utf8mb4 < db/01_schema.sql && "$M" -u servicedesk --default-character-set=utf8mb4 < db/02_seed.sql && echo "БД пересоздана"
