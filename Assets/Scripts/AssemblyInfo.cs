using System.Runtime.CompilerServices;

// The editor assembly (Assets/Tests/Editor, Assets/Scripts/Editor) may use the game's internal members: tests reach
// what they check directly, so a rename fails to compile instead of failing at run time
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
