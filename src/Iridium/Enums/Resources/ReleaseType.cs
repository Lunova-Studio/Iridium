namespace Iridium.Enums.Resources;

[Flags]
public enum ReleaseType {
    Release = 1,
    Beta = 2,
    Alpha = 4,
    All = Release | Beta | Alpha
}
