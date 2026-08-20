namespace Iridium.Enums.Resources;

[Flags]
public enum ResourceSource {
    Modrinth = 1,
    CurseForge = 2,
    All = Modrinth | CurseForge
}
