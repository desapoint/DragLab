using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        internal void ShowIncompatibleSaveDialog(string status)
        {
            root.RemoveFromClassList("hidden");
            OpenDialog("Save reset required",
                "This save uses an incompatible component format that cannot be loaded by the updated tire system. " +
                "A new save is required to continue. The previous files will be archived locally.",
                new[]
                {
                    new DialogAction("Reset and start new game", () =>
                    {
                        if(!game.ResetIncompatibleSave(out var error))
                        {
                            var notice = root.Q<Label>("saveResetError");
                            if(notice != null) notice.text = error;
                        }
                    }, true)
                },
                populate:content =>
                {
                    if(!string.IsNullOrEmpty(status))
                    {
                        var diagnostic = new Label(status) { name="saveResetDetails" };
                        diagnostic.AddToClassList("car-warning");
                        content.Add(diagnostic);
                    }
                    var failure = new Label { name="saveResetError", enableRichText=false };
                    failure.AddToClassList("car-critical");
                    content.Add(failure);
                });
            // It must be impossible to dismiss this modal while the save is blocked.
            var close = root.Q<Button>("modalClose");
            close.SetEnabled(false);
            close.AddToClassList("hidden");
            root.Q("navBar").SetEnabled(false);
        }
    }
}
