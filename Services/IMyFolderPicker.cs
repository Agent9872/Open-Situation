using System.Threading.Tasks;

namespace Lock.Services
{
    public interface IMyFolderPicker
    {
        Task<string> PickFolder();
    }
}