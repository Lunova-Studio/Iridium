namespace Iridium.Interfaces.Resources;


public interface IResourceMirror {

    string Name { get; }


    string? TryRewrite(string url);
}
