#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

var target = Argument("target", "Pack");

string version = "0.0.1";

Setup(context =>
{
    IEnumerable<string> redirectedStandardOutput;
    var exitCodeWithArgument = StartProcess("dotnet", new ProcessSettings 
        {
            Arguments = "run --project src/repo-version/repo-version.csproj --output json",
            RedirectStandardOutput = true
        },
        out redirectedStandardOutput);

    var json = string.Join("\n", redirectedStandardOutput);

    Information(json);

    var repoVersion = ParseJson(json);

    version = repoVersion["SemVer"].ToString();
});

Task("Pack")
    .Does(() =>
    {

        DotNetCorePack(".", new DotNetCorePackSettings
            {
                Configuration = "Release",
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["VERSION"] = version
                }
            });

    });

Task("Publish")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        IEnumerable<string> redirectedStandardOutput;
        var exitCodeWithArgument = StartProcess("git", new ProcessSettings 
            {
                Arguments = "rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true
            },
            out redirectedStandardOutput);

        var branch = redirectedStandardOutput.FirstOrDefault();

        Information($"Current Branch: {branch}");

        var apiKey = EnvironmentVariable("NUGET_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("No value for NUGET_API_KEY");
        }

         var settings = new DotNetCoreNuGetPushSettings
         {
             Source = "https://www.nuget.org/api/v2/package",
             ApiKey = apiKey
         };

         DotNetCoreNuGetPush($"nupkg/repo-version.{version}.nupkg", settings);
    });

Task("Install")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool uninstall -g repo-version"
        });

        StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"tool install -g --add-source ./nupkg repo-version --version {version}"
        });
    });

RunTarget(target);
