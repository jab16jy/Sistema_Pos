<Delta for system-settings>
## ADDED Requirements

### Requirement: Settings view and edit are permission-gated

`LoadSettingsUseCase` MUST require `Settings.View`. `SaveSettingsUseCase` and `UpdateBackupDirectoryUseCase` MUST require `Settings.Edit`. Prior to this change these use cases had zero Application-layer authorization.

#### Scenario: User without Settings.View is denied loading settings
- GIVEN a requesting user whose role lacks `Settings.View`
- WHEN `LoadSettingsUseCase` is invoked
- THEN the use case MUST return an authorization error
- AND no settings data MUST be returned

#### Scenario: User without Settings.Edit is denied saving settings
- GIVEN a requesting user whose role lacks `Settings.Edit`
- WHEN `SaveSettingsUseCase` is invoked with valid settings data
- THEN the use case MUST return an authorization error
- AND the persisted settings MUST remain unchanged

#### Scenario: User with Settings.Edit can update the backup directory
- GIVEN a requesting user whose role has `Settings.Edit`
- WHEN `UpdateBackupDirectoryUseCase` is invoked with a valid path
- THEN the authorization check MUST succeed
- AND the backup directory MUST be persisted

### Requirement: Manual backup creation is permission-gated

`TriggerManualBackupUseCase` MUST require `Admin.Backup.Create`. Prior to this change it had zero Application-layer authorization.

#### Scenario: User without Admin.Backup.Create is denied triggering a backup
- GIVEN a requesting user whose role lacks `Admin.Backup.Create`
- WHEN `TriggerManualBackupUseCase` is invoked
- THEN the use case MUST return an authorization error
- AND no backup file MUST be created

### Requirement: Backup restore is permission-gated

`RestoreBackupUseCase` MUST require `Admin.Backup.Restore`. Prior to this change it had zero Application-layer authorization, making it the least protected and most destructive operation in the system (it overwrites the live database).

#### Scenario: User without Admin.Backup.Restore is denied restoring a backup
- GIVEN a requesting user whose role lacks `Admin.Backup.Restore`
- WHEN `RestoreBackupUseCase` is invoked with a valid backup file
- THEN the use case MUST return an authorization error
- AND the live database MUST remain unmodified
</Delta for system-settings>
