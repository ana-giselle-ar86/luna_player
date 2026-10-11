namespace LunaPlayer.UI;

/// <summary>The whole main-window surface the application drives: the window itself
/// (<see cref="IWindowView"/>) and the menu state (<see cref="IMenuState"/>), with the dialogs reached
/// through <see cref="Dialogs"/> rather than inlined here. <see cref="MainFrame"/> implements the window
/// and menus and owns the <see cref="DialogService"/> behind <see cref="Dialogs"/>.</summary>
internal interface IMainView : IWindowView, IMenuState, IDisposable
{
    /// <summary>Every dialog and prompt the player offers. Kept behind one service rather than on the view
    /// so the main window is not coupled to the concrete dialog classes.</summary>
    IDialogService Dialogs { get; }
}
