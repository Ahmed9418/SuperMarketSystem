using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using Supermarket.Core.Localization;

namespace Supermarket.Wpf;

/// <summary>
/// Installs the Arabic Right-to-Left behaviour of the whole process. Both shells call
/// <see cref="Configure"/> once, at the very top of <c>App.OnStartup</c>, before any window exists.
///
/// <para>
/// Three layers are needed for a text view, a <c>DatePicker</c> and a <c>TextBlock</c> to all agree
/// on the direction:
/// </para>
/// <list type="number">
///   <item><b>Culture</b> — dates, numbers and the <c>DatePicker</c> calendar follow
///   <see cref="AppCulture"/> (ar-EG, Latin digits).</item>
///   <item><b>Language metadata</b> — <c>FrameworkElement.Language</c> is pinned to ar-EG so that
///   font fallback, the <c>DatePicker</c> calendar and the default number formatting of a control
///   all use Arabic rules, including for dialogs created in code.</item>
///   <item><b>Window style</b> — <c>FlowDirection</c>, the Arabic font and the default colours live in
///   the <c>AppWindowStyle</c> resource of each theme (<c>Themes/Controls.xaml</c>,
///   <c>Themes/PosTheme.xaml</c>). See <see cref="PinWindowStyle"/> for why the style is keyed and
///   explicitly attached instead of relying on an implicit style.</item>
/// </list>
/// </summary>
public static class ArabicLayout
{
    /// <summary>XAML key of the window style that carries <c>FlowDirection = RightToLeft</c>.</summary>
    public const string WindowStyleKey = "AppWindowStyle";

    private static readonly object SyncRoot = new();
    private static bool _configured;
    private static bool _windowsPinned;

    /// <summary>
    /// Applies the Arabic culture and the Arabic <c>FrameworkElement.Language</c> to every element
    /// created from now on. Safe to call more than once.
    /// </summary>
    public static void Configure()
    {
        lock (SyncRoot)
        {
            if (_configured)
            {
                return;
            }

            AppCulture.Configure();

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(AppCulture.LanguageTag)));

            _configured = true;
        }
    }

    /// <summary>
    /// Gives every concrete <see cref="Window"/> type of <paramref name="uiAssembly"/> the Arabic
    /// window style as its default.
    ///
    /// <para>
    /// An implicit (keyless) WPF style is matched against the element's <b>exact</b> type, so
    /// <c>&lt;Style TargetType="{x:Type Window}"&gt;</c> in <c>App.xaml</c> never reaches
    /// <c>LoginWindow</c>, <c>MainWindow</c> or any other derived window — the shell would silently
    /// render Left-to-Right. Each window therefore references the style in its own XAML, and this
    /// method is the safety net that covers a window added without it.
    /// </para>
    /// </summary>
    /// <param name="application">The running application (resource lookup + metadata owner).</param>
    /// <param name="uiAssembly">Assembly that holds the windows (normally <c>typeof(App).Assembly</c>).</param>
    public static void PinWindowStyle(Application application, Assembly uiAssembly)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(uiAssembly);

        lock (SyncRoot)
        {
            if (_windowsPinned)
            {
                return;
            }

            if (application.TryFindResource(WindowStyleKey) is not Style style)
            {
                // The theme dictionary is not loaded yet; the explicit Style="..." in every window
                // still applies, so a missing resource must not abort start-up.
                return;
            }

            foreach (var windowType in uiAssembly.GetTypes())
            {
                if (!windowType.IsClass
                    || windowType.IsAbstract
                    || !typeof(Window).IsAssignableFrom(windowType)
                    || windowType == typeof(Window))
                {
                    continue;
                }

                // Metadata can only be overridden once per type; guard for a repeated call
                // (or for a window type that already set its own style default).
                try
                {
                    FrameworkElement.StyleProperty.OverrideMetadata(
                        windowType,
                        new FrameworkPropertyMetadata(style));
                }
                catch (ArgumentException)
                {
                    // Already overridden — leave the existing default in place.
                }
            }

            _windowsPinned = true;
        }
    }
}
