#!/usr/bin/env python3
import sys

with open('workspace/src/Program.cs.bak', 'r') as f:
    lines = f.readlines()

output = []
i = 0
while i < len(lines):
    line = lines[i]
    
    # Replace the comment
    if "// Register data repository" in line:
        output.append("    // Register data repositories\n")
        i += 1
        continue
    
    # Add FocusEventRepository after IUsageRepository
    if "AddScoped<IUsageRepository, UsageRepository>()" in line:
        output.append(line)
        output.append("    builder.Services.AddScoped<FocusEventRepository>();\n")
        i += 1
        continue
    
    # Add telemetry and persistence services before logging services
    if "// Register logging and health services" in line:
        output.append("\n")
        output.append("    // Register telemetry and persistence services\n")
        output.append("    builder.Services.AddSingleton<TelemetryService>();\n")
        output.append("    builder.Services.AddSingleton<PersistenceService>();\n")
        output.append("\n")
        output.append(line)
        i += 1
        continue
    
    output.append(line)
    i += 1

with open('workspace/src/Program.cs', 'w') as f:
    f.writelines(output)

print("Program.cs updated successfully")
