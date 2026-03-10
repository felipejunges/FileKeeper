using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileKeeper.Core.UseCases;

public class RecycleOldBackupUseCase : IRecycleOldBackupUseCase
{
    private readonly IDatabaseService _databaseService;
    private readonly IConfigurationService _configurationService;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<RecycleOldBackupUseCase> _logger;
    private readonly IDeleteBackupUseCase _deleteBackupUseCase;
    
    public RecycleOldBackupUseCase(
        IDatabaseService databaseService,
        IConfigurationService configurationService,
        IBackupRepository backupRepository,
        ILogger<RecycleOldBackupUseCase> logger,
        IDeleteBackupUseCase deleteBackupUseCase)
    {
        _databaseService = databaseService;
        _configurationService = configurationService;
        _backupRepository = backupRepository;
        _logger = logger;
        _deleteBackupUseCase = deleteBackupUseCase;
    }

    public async Task<ErrorOr<int>> ExecuteAsync(CancellationToken cancellationToken)
    {
        int deletedBackups = 0;
        
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);

        while (await VerifyShouldRecycleOldestBackupAsync(configuration, cancellationToken))
        {
            var oldestBackupResult = await _backupRepository.GetOldestAsync(cancellationToken);
            if (oldestBackupResult.IsError)
            {
                _logger.LogWarning("Failed to get oldest backup for recycling: {Errors}", oldestBackupResult.Errors);
                return oldestBackupResult.Errors;
            }

            var oldestBackup = oldestBackupResult.Value;
            
            var deleteResult = await _deleteBackupUseCase.ExecuteAsync(oldestBackup.Id, cancellationToken);
            if (deleteResult.IsError)
                return deleteResult.Errors;
            
            deletedBackups++;
        }

        return deletedBackups;
    }

    private async Task<bool> VerifyShouldRecycleOldestBackupAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        return (await VerifyShouldRecycleOldestBackupWithResultAsync(configuration, cancellationToken))
            .Match(m => m, _ => false);
    }

    private async Task<ErrorOr<bool>> VerifyShouldRecycleOldestBackupWithResultAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        if (configuration.VersionsToKeep <= 0 && configuration.MaxDatabaseSizeMb <= 0)
        {
            _logger.LogInformation("No backup recycling needed: VersionsToKeep={VersionsToKeep}, MaxDatabaseSizeMb={MaxDatabaseSizeMb}",
                configuration.VersionsToKeep,
                configuration.MaxDatabaseSizeMb);
            
            return false;
        }
        
        _logger.LogInformation(
            "Checking if backup recycling is needed: VersionsToKeep={VersionsToKeep}, MaxDatabaseSizeMb={MaxDatabaseSizeMb}",
            configuration.VersionsToKeep,
            configuration.MaxDatabaseSizeMb);
        
        if (configuration.VersionsToKeep > 0)
        {
            var backupsCountResult = await _backupRepository.GetCountAsync(cancellationToken);
            if (backupsCountResult.IsError)
            {
                _logger.LogWarning("Failed to get backups count: {Errors}", backupsCountResult.Errors);
                return backupsCountResult.Errors;
            }

            var backupsCount = backupsCountResult.Value;
            
            if (backupsCount > configuration.VersionsToKeep)
            {
                _logger.LogInformation(
                    "Number of backups ({BackupsCount}) exceeds VersionsToKeep ({VersionsToKeep}), recycling oldest backup.",
                    backupsCount,
                    configuration.VersionsToKeep);
                
                return true;
            }
        }

        if (configuration.MaxDatabaseSizeMb > 0)
        {
            var backupsTotalSizeResult = await _backupRepository.GetAllBackupsTotalSizeAsync(cancellationToken);
            if (backupsTotalSizeResult.IsError)
            {
                _logger.LogWarning("Failed to get backups total size: {Errors}", backupsTotalSizeResult.Errors);
                return backupsTotalSizeResult.Errors;
            }
            
            var databaseSizeMb = backupsTotalSizeResult.Value / (1024.0 * 1024.0);
            if (databaseSizeMb > configuration.MaxDatabaseSizeMb)
            {
                _logger.LogInformation(
                    "Database size ({DatabaseSizeMb:F2} MB) exceeds MaxDatabaseSizeMb ({MaxDatabaseSizeMb:F2} MB), recycling oldest backup.",
                    databaseSizeMb,
                    configuration.MaxDatabaseSizeMb);
                
                return true;
            }
        }

        _logger.LogInformation("No backup recycling needed based on current configuration and database state.");
        
        return false;
    }
}