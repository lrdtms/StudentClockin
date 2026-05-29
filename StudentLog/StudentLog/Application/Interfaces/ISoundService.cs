using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ISoundService
{
    Task PlayAsync(Student student, Cohort cohort, SessionType sessionType);
    Task<string?> UploadSoundAsync();
}
