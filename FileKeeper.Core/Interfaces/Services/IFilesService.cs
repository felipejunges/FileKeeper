using FileKeeper.Core.Models.DTOs;

namespace FileKeeper.Core.Interfaces.Services;

public interface IFilesService
{
    FileToSave CreateFileToSave(string fullFileName);
}