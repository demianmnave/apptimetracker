import json
import os

os.chdir('workspace')

with open('src/appsettings.json', 'r') as f:
    data = json.load(f)

data['TelemetrySettings'] = {
    'EventBufferSize': 1000,
    'FlushIntervalMs': 5000,
    'MaxEventAgeMinutes': 60
}

data['PersistenceSettings'] = {
    'BatchSize': 100,
    'RetryCount': 3,
    'RetryDelayMs': 100
}

with open('src/appsettings.json', 'w') as f:
    json.dump(data, f, indent=2)

print('Updated')
