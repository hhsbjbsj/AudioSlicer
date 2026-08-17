namespace AudioSlicer.Services;

public interface IFileDialogService
{
    string? SelectMediaFile();

    string? SelectProjectToOpen();

    string? SelectProjectToSave(string suggestedName);

    string? SelectExportDirectory();

    string? SelectZipPath(string suggestedName);
}
