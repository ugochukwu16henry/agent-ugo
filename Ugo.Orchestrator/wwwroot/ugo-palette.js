// Agent Ugo — Command Palette keyboard listener and focus helper

/**
 * Registers a global Ctrl+P listener that calls back into Blazor
 * to toggle the UgoCommandPalette. The dotNetRef is a
 * DotNetObjectReference<Dashboard> wired via OnAfterRenderAsync.
 */
window.setupPaletteListener = function (dotNetRef) {
    document.addEventListener('keydown', function (e) {
        if (e.ctrlKey && e.key === 'p') {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('TogglePalette');
        }
    });
};

/**
 * Moves focus to the command palette input element after the
 * palette becomes visible, using a short delay to allow the
 * Blazor render cycle to complete the DOM update.
 */
window.focusPaletteInput = function (element) {
    if (element) {
        // rAF ensures the element is fully rendered before focus
        requestAnimationFrame(function () {
            element.focus();
            // Place cursor at end of any pre-filled value (e.g. ">")
            if (element.value) {
                var len = element.value.length;
                element.setSelectionRange(len, len);
            }
        });
    }
};
