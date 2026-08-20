using System.Diagnostics;
using Iridium;
using Iridium.Download;
using Iridium.Helpers;
using Iridium.Installation;
using Iridium.Launch;
using Iridium.Models;
using Iridium.Models.Launch;
using Iridium.Providers.Java;
using Iridium.Services.Authentication;

IridiumConfig.Configure(new IridiumContext());

Stopwatch stopwatch = Stopwatch.StartNew();

var provider = new JavaProvider();

// Console.WriteLine("Fast scan java:");
//
// var r = FastJavaScanner.Scan()
//     .Distinct();
//
// foreach (var entry in r)
//     Console.WriteLine(entry);


//
// Console.WriteLine();
Console.WriteLine("Full scan java:");

var asyncJava = provider.EnumerableJavaAsync(true);

await foreach (var java in asyncJava)
    Console.WriteLine(java);

stopwatch.Stop();
Console.WriteLine(stopwatch.Elapsed);

return;
// Console.Write("Enter .minecraft folder path: ");
var mcPath = @"D:\Temp\新建文件夹 (9)";

Console.Write("Enter Minecraft version ID: ");
var versionId = Console.ReadLine();

try { 
    var minecraftDir = new DirectoryInfo(mcPath);

    Console.WriteLine("Starting Minecraft installation...");

    // Get all available Minecraft versions
    var minecrafts = await VanillaInstaller.EnumerableMinecraftAsync();
    var minecraft = minecrafts?.FirstOrDefault(x => x.Id.Equals(versionId));
    
    if (minecraft == null) {
        Console.WriteLine($"Error: Version {versionId} not found");
        return;
    }
    
    // Create installer
    VanillaInstaller installer = new(minecraftDir, minecraft, DownloadSource.Official, maxConcurrency: 64);
    
    var watch = Stopwatch.StartNew();
    
    // Progress event handler
    installer.ProgressChanged += (sender, eventArgs) => {
        Console.Clear();
        Console.WriteLine($"TotalProgress: {eventArgs.TotalProgress * 100:0.00}%");
        Console.WriteLine();
        
        foreach (var step in eventArgs.Steps) {
            var status = step.Progress >= 1.0 ? "OK" : $"{step.Progress * 100:0.00}%";
            Console.WriteLine($"[{status}] {step.Name}");
        }
    };
    
    // Execute installation
    var result = await installer.InstallAsync();
    
    Console.WriteLine($"Installation completed in: {watch.Elapsed}");
    watch.Stop();
    
    // Launch the game
    Console.WriteLine();
    
    Console.Write("Enter in-game username: ");
    var username = Console.ReadLine();
    
    Console.Write("Enter Java executable path: ");
    var javaPath = Console.ReadLine();
    
    Console.WriteLine("Launching Minecraft...");
    var launcher = new Launcher();
    
    var process = await launcher.LaunchAsync(result.Entry, new LaunchConfig {
        Account = new OfflineAuthenticator().Authenticate(username),
        JavaPath = await new JavaProvider().GetJavaEntryAsync(javaPath),
        MaxMemorySize = 4096
    });

    Console.WriteLine($"Arguments: {string.Join('\n', process.ArgumentList)}");
    
    process.OutputLogReceived += (s, e) => Console.WriteLine(e.Data);
    process.Process?.WaitForExit();
    
    Console.WriteLine();
    Console.WriteLine("Game exited");
}
catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();