using System.Threading.Tasks;

namespace Lock.Services
{
    public interface IFolderPicker
    {
        Task<string?> PickFolder();
        Task<bool> CanWriteToFolder(string folderIdentifier);
    }
}