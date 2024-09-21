using System.Formats.Tar;
using System.Text.Json;

// Get work files and folders
if (args.Length > 2 || args.Length <= 0)
{
    Console.WriteLine("Syntax: OciLxcConverter <Source OCI tar file> [<output directory>]");
    Environment.Exit(-1);
}

Console.WriteLine("Checking archive");
var archive = args[0];
var output = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();
var dir = Directory.CreateTempSubdirectory();

try
{
    // Extract tar file into a folder with the same name.
    Console.WriteLine("Extract archive");
    await TarFile.ExtractToDirectoryAsync(archive, dir.FullName, true);
    Directory.SetCurrentDirectory(dir.FullName);

    // Look for the index.json and get the config and image paths
    Console.WriteLine("look for index.json");
    var index = JsonDocument.Parse(File.ReadAllText("index.json")).RootElement;

    Console.WriteLine("Look for entrypoint script and image archive");
    var manifestPath = Path.Combine(
        "blobs",
        index.GetProperty("manifests")[0].GetProperty("digest").GetString()!.Replace(':', Path.DirectorySeparatorChar));
    var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath)).RootElement;

    var imageFile = Path.Combine(
        "blobs",
        manifest.GetProperty("layers")[0].GetProperty("digest").GetString()!.Replace(':', Path.DirectorySeparatorChar));

    var configFile = Path.Combine(
        "blobs",
        manifest.GetProperty("config").GetProperty("digest").GetString()!.Replace(':', Path.DirectorySeparatorChar));
    var config = JsonDocument.Parse(File.ReadAllText(configFile)).RootElement;

    if (!Directory.Exists(output))
    {
        Directory.CreateDirectory(output);
    }

    // Move and rename the image file to the output folder and add entrypoint script.
    Console.WriteLine("Saving entrypoint script and image");
    var entrypoint = string.Join(" ", config.GetProperty("config").GetProperty("Entrypoint").EnumerateArray().Select(x => x.GetString()));
    var envVars = string.Join("\n", config.GetProperty("config").GetProperty("Env").EnumerateArray().Select(x => x.GetString()));
    var newImagePath = Path.Combine(output, new FileInfo(archive).Name.Split('.')[0] + ".tar.gz");
    var newEntrypointFile = Path.Combine(output, "entrypoint.sh");
    var newEnvVarsFile = Path.Combine(output, "env.txt");
    File.Copy(imageFile, newImagePath, true);
    File.WriteAllText(newEntrypointFile, entrypoint);
    File.WriteAllText(newEnvVarsFile, envVars);
    
    // Garbage collect
    Console.WriteLine("Garbage collecting");
    
    Directory.SetCurrentDirectory(output);
    File.Delete(archive);
    
    Console.WriteLine("All done :)");
}
catch (Exception ex)
{
    Directory.SetCurrentDirectory(output);
    Console.Error.WriteLine("Could not convert the package. Check stacktrace bellow");
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(ex.StackTrace);
}

dir.Delete(true);