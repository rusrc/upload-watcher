# upload-watcher


## Настройка

В `appsettings.json` нужно заменить только секцию `MonitorSettings`.

`DirectoryPath` — физический путь к файлам.

```json
{
  "MonitorSettings": {
    "DirectoryPath": "D:\\GitRepos\\UpdateWatcher\\Files",
    "PollingIntervalSeconds": 5
  }
}
```
