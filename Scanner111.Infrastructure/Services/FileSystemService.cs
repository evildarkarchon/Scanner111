using Scanner111.Core.Interfaces.Services;

namespace Scanner111.Infrastructure.Services;

public class FileSystemService : IFileSystemService
{
    public async Task<bool> FileExistsAsync(string path)
    {
        return await Task.FromResult(File.Exists(path));
    }
    
    public async Task<bool> DirectoryExistsAsync(string path)
    {
        return await Task.FromResult(Directory.Exists(path));
    }
    
    public async Task<string> ReadAllTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }
    
    public async Task WriteAllTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content);
    }
    
    public async Task<byte[]> ReadAllBytesAsync(string path)
    {
        return await File.ReadAllBytesAsync(path);
    }
    
    public async Task WriteAllBytesAsync(string path, byte[]? bytes)
    {
        if (bytes != null)
            await File.WriteAllBytesAsync(path, bytes);
    }
    
    public async Task<string[]> GetFilesAsync(string path, string searchPattern, bool recursive = false)
    {
        return await Task.FromResult(
            Directory.GetFiles(
                path,
                searchPattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
    }
    
    public async Task<string[]> GetDirectoriesAsync(string path, string searchPattern, bool recursive = false)
    {
        return await Task.FromResult(
            Directory.GetDirectories(
                path,
                searchPattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
    }
    
    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
    {
        using var sourceStream = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        using var destinationStream = new FileStream(
            destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await sourceStream.CopyToAsync(destinationStream);
    }
    
    public async Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        File.Move(sourcePath, destinationPath);
        await Task.CompletedTask;
    }
    
    public async Task CreateDirectoryAsync(string path)
    {
        Directory.CreateDirectory(path);
        await Task.CompletedTask;
    }
    
    public async Task DeleteFileAsync(string path)
    {
        File.Delete(path);
        await Task.CompletedTask;
    }
    
    public async Task DeleteDirectoryAsync(string path, bool recursive = true)
    {
        Directory.Delete(path, recursive);
        await Task.CompletedTask;
    }
}