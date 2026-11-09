# تقرير المسح والتحليل المعماري لشاشات الإدارة والإعدادات ودعم RTL العربي
# UI Layout, Settings View, RTL Arabic Localization & Backup Tab Integration Analysis

**تاريخ المسح:** 2026-11-09  
**المستكشف:** `teamwork_explorer_survey_2`  
**المشروع:** `SupermarketSystem` — تطبيق إدارة السوبر ماركت (`Supermarket.Admin`)  
**الهدف:** فحص تفصيلي لهيكل واجهات الإدارة (Admin Views)، شاشة الإعدادات (Settings View)، نظام القوالب والأنماط (Styles & Controls)، آليات الدعم الكامل للغة العربية والاتجاه من اليمين لليسار (RTL Localization)، صياغة ضوابط عناصر التحكم (Cards, Dividers, Buttons, ComboBox, ToggleSwitch)، وأنماط الحوارات والتنبيهات (Dialog Patterns)، وتحديد نقطة التكامل الدقيقة والمواصفات التصميمية لإضافة تبويب «النسخ الاحتياطي واستعادة قاعدة البيانات» (Database Backup & Recovery).

---

## 1. الملخص التنفيذي (Executive Summary)

1. **هيكل واجهة المستخدم الحالية:** تطبيق الإدارة مبني بتقنية WPF على .NET 8 (`net8.0-windows`) ومصمم بنمط MVVM كامل مع مكتبة `CommunityToolkit.Mvvm`. التنقل بين الصفحات يتم في `MainWindow.xaml` عبر `ContentControl` يربط `CurrentViewModel` بنماذج عرض (`DataTemplates`).
2. **شاشة الإعدادات الحالية (`SettingsView.xaml`):** تتألف حالياً من حاوية `ScrollViewer` واحدة تعرض بطاقات إعدادات المتجر وبيانات الفاتورة والطباعة ومعاينة حية للإيصال ومعلومات النظام. لا تحتوي حالياً على تبويبات (`TabControl`)، ولكن نمط التبويبات مُعرّف ومُطبّق بالفعل بنجاح في شاشة الأقسام والكتالوج (`CatalogView.xaml`).
3. **نظام الأنماط والقوالب (Styling Framework):** لا يعتمد التطبيق على مكتبات أطراف ثالثة خارجية ثقيلة (مثل MaterialDesign أو ModernWpf)، بل يمتلك نظام أنماط موحد ونظيف جداً في `Themes/Colors.xaml` و `Themes/Controls.xaml`. تم توثيق قوالب البطاقات (`CardBorderStyle`)، عناوين البطاقات (`CardTitleTextStyle`)، تسميات الحقول (`FieldLabelTextStyle`)، والأزرار (`PrimaryButtonStyle`, `SecondaryButtonStyle`, `DangerButtonStyle`).
4. **عنصر التبديل (ToggleSwitch):** نظراً لعدم وجود عنصر `ToggleSwitch` مدمج في WPF الخام، فإن أفضل ممارسة متبعة هي إنشاء `Style` مخصص للعنصر `CheckBox` (أو `ToggleButton`) بقالب `ControlTemplate` حديث يُمثل زر تبديل انزلاقي (Sliding Pill Switch) بمظهر Fluent/iOS مع الحفاظ على خصائص الربط `IsChecked`.
5. **الاتجاه العربي (RTL) والأرقام اللاتينية:** التطبيق يطبق قاعدة مركزية صارمة موثقة في `docs/RTL-VERIFICATION.md`: يُضبط `FlowDirection = RightToLeft` مرة واحدة على نمط النافذة `AppWindowStyle`، وتورثه جميع عناصر التحكم التابعة. النصوص اللاتينية ومسارات الملفات (`C:\Backups\...`) تُعزل صراحةً باستخدام `LatinInputStyle` أو `RtlIsolation` حتى لا تنعكس علامات المسارات والرموز.
6. **مربعات الحوار وتأكيدات الأمان (Dialog Patterns):** يستخدم التطبيق `IDialogService` كواجهة مجردة موحدة تستند إلى `ArabicMessageBox` الذي يمرر دائماً `MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign` مع جعل زر الرفض (`No`) هو الافتراضي لعمليات التأكيد الحساسة.
7. **نقطة التكامل الموصى بها (Integration Point):** تحويل المحتوى الرئيسي في `SettingsView.xaml` إلى `TabControl` يحتوي على تبويبين:
   - **التبويب الأول:** «🏪 الإعدادات العامة والطباعة» (General Settings & Printing) — يشمل المحتوى الحالي بالكامل.
   - **التبويب الثاني:** «💾 النسخ الاحتياطي واستعادة البيانات» (Database Backup & Recovery) — يضم 3 بطاقات رئيسية (حالة النظام وآخر نسخة احتياطية، النسخ والاستعادة اليدوية، والنسخ الاحتياطي التلقائي المجدول).

---

## 2. تحليل هيكل شاشات الإدارة ونظام التنقل (Admin Views & Navigation Architecture)

### 2.1 شل التطبيق الرئيسي (`MainWindow.xaml` & `MainWindow.xaml.cs`)
- **الملف:** `src/Supermarket.Admin/Views/MainWindow.xaml`
- **نمط الاتجاه:** النافذة ترث `AppWindowStyle` مما يجعل الاتجاه العام `RightToLeft` تلقائياً.
- **الشريط الجانبي (Sidebar):** يقع في العمود الأول `ColumnDefinition Width="270"` ويظهر على **اليمين بصرياً** بسبب اتجاه RTL.
- **عرض الصفحات (Page Hosting):**
  في السطر 200:
  ```xml
  <ContentControl Grid.Row="1" Content="{Binding Navigation.CurrentViewModel}" Margin="16" />
  ```
- **ربط النماذج بالواجهات (DataTemplates):**
  في السطور 16–42، يتم تعريف الربط بين أنواع الـ ViewModels والـ Views المقابلة:
  ```xml
  <DataTemplate DataType="{x:Type vm:DashboardViewModel}"><views:DashboardView /></DataTemplate>
  <DataTemplate DataType="{x:Type vm:ProductsViewModel}"><views:ProductsView /></DataTemplate>
  <DataTemplate DataType="{x:Type vm:SettingsViewModel}"><views:SettingsView /></DataTemplate>
  ```
  هذا يعني أن شاشة الإعدادات تُفتح وتُدار بالكامل عبر `SettingsViewModel` عندما يضغط المستخدم على زر «⚙ الإعدادات» المرتبط بـ `NavigateToSettingsCommand`.

### 2.2 شاشة الإعدادات الحالية (`SettingsView.xaml`)
- **الملف:** `src/Supermarket.Admin/Views/SettingsView.xaml` (634 سطراً).
- **الهيكل الحالي:**
  - `Grid` مكوّن من صفين:
    - `Row 0` (`Height="*"`): حاوية تمرير `ScrollViewer` تحتوي على:
      - `Grid` بعمودين (الأسطر 19–176):
        - العمود الأيمن (`Grid.Column="0"`): بطاقة «بيانات المتجر (تُطبع على الإيصال)» وتشمل اسم المتجر، العنوان، الهاتف، الرقم الضريبي، رسالة التذييل، واختيار الشعار (`LogoPath`).
        - العمود الأيسر (`Grid.Column="1"`): بطاقة «الطباعة والضريبة» وتشمل نوع الفاتورة، اسم الطابعة، عرض السطر، نسبة الضريبة، وخيارات الأسعار.
      - بطاقة «📄 شكل وتصميم الفاتورة (معاينة حية)» (الأسطر 179–590): معاينة ديناميكية لإيصال 80 مم وفاتورة A4/A5.
      - بطاقة «معلومات النظام» (الأسطر 592–604): تعرض رقم الإصدار والملاحظات.
    - `Row 1` (`Height="Auto"`): شريط الأوامر السفلي (Bottom Actions Bar - الأسطر 609–631):
      - عرض رسالة الحالة `StatusMessage`.
      - زر «👁️ عرض/إخفاء شكل الفاتورة» (`TogglePreviewCommand`).
      - زر «💾 حفظ الإعدادات» (`SaveCommand`).
      - زر «🔄 إعادة التحميل» (`LoadCommand`).

### 2.3 نموذج عرض الإعدادات (`SettingsViewModel.cs`)
- **الملف:** `src/Supermarket.Admin/ViewModels/SettingsViewModel.cs` (381 سطراً).
- **الوراثة والنمط:** `public sealed partial class SettingsViewModel : ObservableObject` باستخدام `CommunityToolkit.Mvvm`.
- **التبعيات المحقونة الحالية:**
  ```csharp
  public SettingsViewModel(
      IStoreSettingsService settings,
      IReportService reports,
      IDialogService dialogs,
      AuthSession session)
  ```
- **الصلاحيات:** يفحص خاصية `CanEdit`:
  ```csharp
  public bool CanEdit => _session.Can(AppPermissions.SettingsEdit) || _session.IsAdministrator;
  ```
- **استخدام الحوارات ومربعات الملفات الحالية:**
  في السطر 168 (أمر `ChooseLogoCommand`):
  يتم استدعاء `Microsoft.Win32.OpenFileDialog` لاختيار ملفات الصور.
  ويتم استخدام `_dialogs.ShowInformation` و `_dialogs.ShowWarning` و `_dialogs.ShowError`.

---

## 3. دراسة نظام الأنماط وعناصر التحكم (WPF Styles & Control Toolkit)

### 3.1 لوحة الألوان والرموز (`Themes/Colors.xaml`)
تحتوي على لوحة ألوان عصرية ومهنية ("Modern Blue"):
- `PrimaryBrush` (`#1D4ED8`): اللون الأساسي للأزرار والإجراءات الفعالة.
- `PrimaryDarkBrush` (`#1E3A8A`): لعناوين الصفحات ورؤوس البطاقات والجداول.
- `PrimaryLightBrush` (`#EFF6FF`): خلفيات ناعمة للبطاقات والأزرار الثانوية.
- `AccentBrush` (`#F59E0B`): تمييز التحذيرات والمبالغ.
- `DangerBrush` (`#DC2626`): لعمليات الحذف والاستعادة الخطرة.
- `WarningBrush` (`#D97706`): للتنبيهات والتحذيرات.
- `SuccessBrush` (`#059669`): لحالات النجاح والنسخ الاحتياطي المكتمل.
- `SurfaceBrush` (`#FFFFFF`): أسطح البطاقات والنوافذ.
- `CanvasBrush` (`#F4F6FB`): خلفية التطبيق العامة.
- `BorderBrush` (`#DDE3EE`): الحدود وفواصل الأقسام.
- `CardCornerRadius`: `10`
- `CardPadding`: `16`

### 3.2 عناصر التحكم الجاهزة في `Themes/Controls.xaml`
1. **قالب البطاقات (`CardBorderStyle`):**
   ```xml
   <Style x:Key="CardBorderStyle" TargetType="{x:Type Border}">
       <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
       <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
       <Setter Property="BorderThickness" Value="1" />
       <Setter Property="CornerRadius" Value="{StaticResource CardCornerRadius}" />
       <Setter Property="Padding" Value="{StaticResource CardPadding}" />
       <Setter Property="SnapsToDevicePixels" Value="True" />
   </Style>
   ```
2. **عناوين البطاقات (`CardTitleTextStyle`):**
   ```xml
   <Style x:Key="CardTitleTextStyle" TargetType="{x:Type TextBlock}">
       <Setter Property="FontSize" Value="{StaticResource FontSizeMedium}" />
       <Setter Property="FontWeight" Value="Bold" />
       <Setter Property="Foreground" Value="{StaticResource PrimaryDarkBrush}" />
       <Setter Property="Margin" Value="0,0,0,10" />
   </Style>
   ```
3. **تسميات الحقول (`FieldLabelTextStyle`):**
   ```xml
   <Style x:Key="FieldLabelTextStyle" TargetType="{x:Type TextBlock}">
       <Setter Property="FontSize" Value="{StaticResource FontSizeSmall}" />
       <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}" />
       <Setter Property="Margin" Value="0,0,0,4" />
   </Style>
   ```
4. **أنماط الأزرار المتوفرة:**
   - `BaseButtonStyle` / `PrimaryButtonStyle`: أزرار رئيسية باللون الأزرق الداكن وزوايا `CornerRadius="6"`.
   - `SecondaryButtonStyle`: أزرار باللون الأزرق الفاتح (`PrimaryLightBrush`).
   - `DangerButtonStyle`: أزرار حمراء باللون `DangerBrush` تُستخدم للعمليات التدميرية (مثل استعادة قاعدة البيانات أو الحذف).
   - `GhostButtonStyle`: زر بإطار شفاف.
5. **نمط التبويبات الحالي (`TabControl` & `TabItem`):**
   في `Controls.xaml` السطور 324–335:
   ```xml
   <Style TargetType="{x:Type TabControl}">
       <Setter Property="FontFamily" Value="{StaticResource AppFontFamily}" />
       <Setter Property="Background" Value="Transparent" />
       <Setter Property="BorderThickness" Value="0" />
   </Style>

   <Style TargetType="{x:Type TabItem}">
       <Setter Property="FontFamily" Value="{StaticResource AppFontFamily}" />
       <Setter Property="FontSize" Value="15" />
       <Setter Property="FontWeight" Value="SemiBold" />
       <Setter Property="Padding" Value="14,6" />
   </Style>
   ```
   **المرجع الفعلي:** شاشة `CatalogView.xaml` (السطر 37) تستخدم هذا النمط بالضبط للفصل بين «الأقسام» و«وحدات القياس»، مما يمنحها تكاملاً بصرياً متناسقاً تماماً.

### 3.3 مواصفات عنصر التبديل (ToggleSwitch Specification)
حيث أن المشروع يعتمد على WPF النقي بدون مكاتب خارجية، يجب توفير ستايل مخصص لـ `CheckBox` باسم `ToggleSwitchStyle` يمكن وضعه في `Themes/Controls.xaml` ليخدم هذا المتطلب وأي شاشات أخرى مستقبلاً:

```xml
<!-- Modern Fluent/iOS Toggle Switch Style for CheckBox -->
<Style x:Key="ToggleSwitchStyle" TargetType="{x:Type CheckBox}">
    <Setter Property="FontFamily" Value="{StaticResource AppFontFamily}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeNormal}" />
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type CheckBox}">
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <!-- Switch Pill (Forced LTR so the thumb translates consistently Left-to-Right) -->
                    <Grid Width="46" Height="24" FlowDirection="LeftToRight" VerticalAlignment="Center" Margin="0,0,10,0">
                        <Border x:Name="Track"
                                Background="#CBD5E1"
                                CornerRadius="12"
                                BorderThickness="0"
                                SnapsToDevicePixels="True">
                            <Ellipse x:Name="Thumb"
                                     Width="18" Height="18"
                                     Fill="White"
                                     HorizontalAlignment="Left"
                                     Margin="3,0,0,0"
                                     VerticalAlignment="Center">
                                <Ellipse.Effect>
                                    <DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.25" Direction="270" />
                                </Ellipse.Effect>
                            </Ellipse>
                        </Border>
                    </Grid>
                    <!-- Label Text -->
                    <ContentPresenter VerticalAlignment="Center" SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}" />
                </StackPanel>
                <ControlTemplate.Triggers>
                    <!-- Checked state -->
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="Track" Property="Background" Value="{StaticResource PrimaryBrush}" />
                        <Setter TargetName="Thumb" Property="HorizontalAlignment" Value="Right" />
                        <Setter TargetName="Thumb" Property="Margin" Value="0,0,3,0" />
                    </Trigger>
                    <!-- Hover state -->
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Track" Property="Opacity" Value="0.92" />
                    </Trigger>
                    <!-- Disabled state -->
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Opacity" Value="0.5" />
                        <Setter Property="Cursor" Value="No" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 4. تحليل معايير العرض العربي (RTL) والأرقام ومربعات الحوار

### 4.1 القواعد المعمارية لـ RTL في النظام
1. **عدم تكرار `FlowDirection="RightToLeft"` في كل `UserControl`:**
   يتم توريث الاتجاه تلقائياً من النافذة الأم `Window`. إذا كُتبت الخاصية يدوياً في كل عنصر، تسبب تعارضات تشوه محاذاة عناصر التمرير وشبكات البيانات.
2. **عزل النصوص اللاتينية ومسارات الملفات (Latin Isolation):**
   مسار الملفات مثل `C:\Supermarket\Backups\SupermarketDb_20261109.bak` إذا عُرض داخل حاوية عربية قد تنعكس فيه الفواصل المائلة (`\`) وتظهر الأحرف مقلوبة.
   **الحل القياسي في التطبيق:**
   - استخدام `Style="{StaticResource LatinInputStyle}"` لحقل مسار المجلد والملفات، والذي يضبط صراحةً:
     ```xml
     FlowDirection="LeftToRight"
     TextAlignment="Left"
     FontFamily="Consolas, Segoe UI, Tahoma"
     ```
   - أو استخدام محول `RtlIsolationConverter` (`{Binding Path, Converter={StaticResource RtlIsolation}}`) الذي يحيط النص بعلامات `\u200E` (LRM).
3. **الأرقام والتواريخ:**
   وفقاً لـ `AppCulture.cs`، التقويم ميلادي والأرقام لاتينية دائماً (`1,234.50`) لضمان الوضوح التام مع أسماء الملفات وأجهزة الباركود.

### 4.2 نمط مربعات الحوار والتأكيدات (Dialog Pattern)
- **خدمة الحوارات `IDialogService`:**
  - `ShowInformation(message, title)`: لعرض نجاح عملية النسخ الاحتياطي ومسار الملف المكتمل.
  - `ShowWarning(message, title)`: لعرض تنبيهات التحقق (مثل مسار غير صالح، أو فشل قراءة المجلد).
  - `ShowError(message, title)`: لعرض أي خطأ استثنائي أثناء النسخ أو الاستعادة.
  - `Confirm(message, title)`: لطلب تأكيد أمني قطعي من المستخدم قبل الشروع في استعادة قاعدة البيانات:
    ```csharp
    bool confirmed = _dialogs.Confirm(
        "تحذير أمني شديد: استعادة قاعدة البيانات ستؤدي إلى استبدال كافة البيانات الحالية بالبيانات الموجودة في ملف النسخة الاحتياطية وإغلاق كافة الاتصالات النشطة.\n\nهل أنت متأكد تماماً من المتابعة؟",
        "تأكيد استعادة قاعدة البيانات");
    if (!confirmed) return;
    ```
- **محاذاة مربعات الرسائل العربية:**
  كلاس `ArabicMessageBox.cs` يضمن أن جميع رسائل التأكيد والخطأ تستخدم `MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign` مع تعيين الزر الافتراضي على `MessageBoxResult.No` في التأكيدات لدرء ضغط المستخدم الخاطئ.

---

## 5. مواصفات ونقطة تكامل تبويب «النسخ الاحتياطي واستعادة قاعدة البيانات»

### 5.1 نقطة التكامل في `SettingsView.xaml`
تحويل المحتوى الحالي في `Grid.Row="0"` إلى `TabControl`:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <!-- Navigation Tabs -->
    <TabControl Grid.Row="0" Margin="0,4,0,0">

        <!-- ==================== TAB 1: General & Printing ==================== -->
        <TabItem Header="⚙   الإعدادات العامة والطباعة">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="0,12,0,16">
                    <!-- المحتوى الحالي لبيانات المتجر والطباعة والمعاينة ومعلومات النظام -->
                    ...
                </StackPanel>
            </ScrollViewer>
        </TabItem>

        <!-- ==================== TAB 2: Database Backup & Recovery ==================== -->
        <TabItem Header="💾   النسخ الاحتياطي واستعادة البيانات">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="0,12,0,16">
                    <!-- بطاقات النسخ الاحتياطي والاستعادة والجدولة التلقائية -->
                    ...
                </StackPanel>
            </ScrollViewer>
        </TabItem>

    </TabControl>

    <!-- Bottom Actions Bar (يبقى ثابتاً في الأسفل) -->
    <Border Grid.Row="1" Margin="0,12,0,-12" Padding="14,8" ...>
        ...
    </Border>
</Grid>
```

### 5.2 تصميم وبنية بطاقات التبويب الثاني (XAML Specifications)

يتكون التبويب الثاني من 3 بطاقات نظيفة ومنظمة وفق نظام بطاقات `CardBorderStyle`:

#### 1. بطاقة حالة قاعدة البيانات وآخر نسخ احتياطي (Overview & Status Card)
- **الغرض:** تقديم مؤشر فوري لحالة الاتصال، اسم المحرك (`SQL Server - localhost\SupermarketDb`)، وتاريخ آخر عملية نسخ ناجحة، مع شريط تقدم أثناء تنفيذ العمليات الطويلة.
- **عناصر البطاقة:**
  - عنوان: `حالة قاعدة البيانات والنسخ الاحتياطي` (`CardTitleTextStyle`).
  - ملخص معلومات:
    - نوع قاعدة البيانات: `Microsoft SQL Server`
    - اسم قاعدة البيانات: `SupermarketDb`
    - آخر نسخة احتياطية ناجحة: مرتبط بـ `LastBackupTimestampText` (مثال: `2026/11/09 15:30:00` أو `لا توجد نسخة مسجلة بعد`).
    - رسالة الحالة الحالية: مرتبط بـ `BackupStatusMessage`.
  - شريط تقدم indeterminate يعمل عند `IsBackupInProgress` أو `IsRestoreInProgress`.

#### 2. بطاقة النسخ الاحتياطي والاستعادة اليدوية (Manual Section Card)
- **الغرض:** تلبية المتطلب R2 بشكل دقيق مع أزرار بارزة ومحاذاة RTL كاملة.
- **التصميم المقترح:**
  تقسيم البطاقة إلى قسمين واضحين يفصل بينهما خط فاصل:
  - **القسم الأول (إنشاء نسخة احتياطية فورية):**
    - الوصف: `إنشاء نسخة احتياطية كاملة (.bak) تشمل كافة المنتجات، الفواتير، الحسابات، والمستخدمين وحفظها في الموقع الذي تختاره.`
    - الزر البارز:
      ```xml
      <Button Content="💾   إنشاء نسخة احتياطية الآن"
              Style="{StaticResource PrimaryButtonStyle}"
              Height="42" Padding="20,0" FontSize="14"
              Command="{Binding BackupNowCommand}"
              IsEnabled="{Binding CanPerformBackup}" />
      ```
      - عند النقر: يفتح `SaveFileDialog` مع اسم افتراضي مقترح `SupermarketDb_Backup_yyyyMMdd_HHmmss.bak`.
      - ينفذ العملية بشكل Asynchronous (`async/await`) مع تحديث واجهة المستخدم فور الانتهاء.
  - **القسم الثاني (استعادة قاعدة البيانات):**
    - شارة تحذيرية: خلفية بلون `WarningLightBrush` ونص يوضح أثر الاستعادة.
    - الوصف: `استعادة كاملة لقاعدة البيانات من ملف نسخة احتياطية سابق (.bak). تحذير: ستستبدل هذه العملية كافة البيانات الحالية بالبيانات الموجودة داخل الملف.`
    - الزر البارز:
      ```xml
      <Button Content="⚠️   استعادة قاعدة البيانات"
              Style="{StaticResource DangerButtonStyle}"
              Height="42" Padding="20,0" FontSize="14"
              Command="{Binding RestoreDatabaseCommand}"
              IsEnabled="{Binding CanPerformRestore}" />
      ```
      - عند النقر: يفتح `OpenFileDialog` لاختيار ملف `.bak` صالح.
      - يطلب فوراً تأكيداً أمنياً صريحاً عبر `_dialogs.Confirm(...)`.
      - عند الموافقة: يحرر كافة الاتصالات النشطة، ويحول القاعدة إلى `SINGLE_USER`، ثم يستعيد البيانات، ويعيدها إلى `MULTI_USER` ويفرغ برك الاتصال `SqlConnection.ClearAllPools()`.

#### 3. بطاقة النسخ الاحتياطي التلقائي المجدول (Auto-Backup Section Card)
- **الغرض:** تلبية المتطلب R2 الخاص بجدولة النسخ التلقائي:
- **عناصر التحكم:**
  1. **عنصر التبديل ToggleSwitch:**
     ```xml
     <CheckBox Style="{StaticResource ToggleSwitchStyle}"
               IsChecked="{Binding IsAutoBackupEnabled, Mode=TwoWay}"
               Content="التسجيل الاحتياطي التلقائي"
               FontSize="14" FontWeight="SemiBold"
               Margin="0,6,0,12" />
     ```
  2. **قائمة تكرار النسخ ComboBox:**
     ```xml
     <TextBlock Text="تكرار النسخ الاحتياطي التلقائي *" Style="{StaticResource FieldLabelTextStyle}" />
     <ComboBox ItemsSource="{Binding BackupFrequencies}"
               SelectedItem="{Binding SelectedBackupFrequency, Mode=TwoWay}"
               Height="36" Width="260" HorizontalAlignment="Right"
               IsEnabled="{Binding IsAutoBackupEnabled}"
               Margin="0,0,0,12" />
     ```
     - الخيارات: «يومي» (Daily) و «أسبوعي» (Weekly).
  3. **حقل مسار مجلد الحفظ مع زر الاستعراض (Browse Combo):**
     ```xml
     <TextBlock Text="مجلد حفظ النسخ الاحتياطية التلقائية *" Style="{StaticResource FieldLabelTextStyle}" />
     <Grid Margin="0,0,0,14">
         <Grid.ColumnDefinitions>
             <ColumnDefinition Width="*" />
             <ColumnDefinition Width="Auto" />
         </Grid.ColumnDefinitions>
         <TextBox Grid.Column="0"
                  Style="{StaticResource LatinInputStyle}"
                  Text="{Binding AutoBackupFolderPath, UpdateSourceTrigger=PropertyChanged}"
                  IsEnabled="{Binding IsAutoBackupEnabled}"
                  Height="38" Margin="0,0,8,0" />
         <Button Grid.Column="1"
                 Content="📁   استعراض..."
                 Style="{StaticResource SecondaryButtonStyle}"
                 Width="130" Height="38"
                 Command="{Binding BrowseAutoBackupFolderCommand}"
                 IsEnabled="{Binding IsAutoBackupEnabled}" />
     </Grid>
     ```
  4. **زر حفظ إعدادات النسخ التلقائي:**
     ```xml
     <Button Content="💾   حفظ إعدادات النسخ التلقائي"
             Style="{StaticResource PrimaryButtonStyle}"
             Width="220" Height="38" HorizontalAlignment="Right"
             Command="{Binding SaveAutoBackupSettingsCommand}"
             IsEnabled="{Binding CanEdit}" />
     ```

---

## 6. عقد البيانات والربط في ViewModel (Binding & Contract Design)

لضمان الحفاظ على نظافة الكود وفصل المسؤوليات، نوصي باستخدام كلاس جزئي `SettingsViewModel.Backup.cs` يُكمل `SettingsViewModel.cs` الحالي دون تضخيمه:

### 6.1 الخصائص المطلوبة للواجهة (Observable Properties)
| الخاصية | النوع | الغرض وعلاقة الربط |
|---|---|---|
| `IsAutoBackupEnabled` | `bool` | حالة تفعيل أو تعطيل النسخ الاحتياطي التلقائي (ربط مع الـ ToggleSwitch) |
| `SelectedBackupFrequency` | `string` | تكرار النسخ المختار ("يومي" أو "أسبوعي") |
| `BackupFrequencies` | `IReadOnlyList<string>` | قائمة خيارات التكرار: `new[] { "يومي", "أسبوعي" }` |
| `AutoBackupFolderPath` | `string` | مسار المجلد الذي ستُحفظ فيه النسخ التلقائية |
| `LastBackupTimestampText` | `string` | نص منسق لعرض تاريخ ووقت آخر نسخة ناجحة |
| `BackupStatusMessage` | `string` | رسالة حالة تفاعلية تظهر للعميل توضح ما يجري لحظياً |
| `IsBackupInProgress` | `bool` | يُرفع أثناء تنفيذ النسخ اليدوي لتعطيل الأزرار وإظهار مؤشر الانتظار |
| `IsRestoreInProgress` | `bool` | يُرفع أثناء تنفيذ الاستعادة لتعطيل الواجهة ومنع أي تداخل |

### 6.2 الأوامر البرمجية (Relay Commands)
1. `BackupNowCommand` (`async Task BackupNowAsync()`):
   - يفتح `SaveFileDialog`.
   - يستدعي `IBackupService.CreateBackupAsync(targetFilePath)`.
   - يحدّث مؤشرات الحالة وتاريخ آخر نسخة.
   - يعرض رسالة نجاح عبر `_dialogs.ShowInformation`.
2. `RestoreDatabaseCommand` (`async Task RestoreDatabaseAsync()`):
   - يفتح `OpenFileDialog`.
   - يطلب تأكيد المستخدم الحاسم عبر `_dialogs.Confirm`.
   - يستدعي `IBackupService.RestoreDatabaseAsync(backupFilePath)`.
   - يعرض تنبيه النجاح أو الخطأ.
3. `BrowseAutoBackupFolderCommand` (`void BrowseAutoBackupFolder()`):
   - يفتح نافذة اختيار المجلد (باستخدام `Microsoft.Win32.OpenFolderDialog` في .NET 8).
   - يسند المسار المختار إلى `AutoBackupFolderPath`.
4. `SaveAutoBackupSettingsCommand` (`async Task SaveAutoBackupSettingsAsync()`):
   - يتحقق من صحة المسار ووجوده.
   - يحفظ الإعدادات في التخزين الدائم لتبقى بين جلسات التشغيل.

---

## 7. خارطة طريق التعديلات البرمجية (Implementation Roadmap)

| الملف | نوع التعديل | التفاصيل |
|---|---|---|
| `src/Supermarket.Admin/Themes/Controls.xaml` | إضافة ستايل | إضافة `ToggleSwitchStyle` لعنصر `CheckBox` لتمكين التبديل الانزلاقي الحديث. |
| `src/Supermarket.Admin/Views/SettingsView.xaml` | تعديل XAML | تحويل الحاوية لـ `TabControl`، نقل محتوى الإعدادات الحالية لتبويب 1، وإضافة تبويب 2 بالبطاقات الثلاث الموصوفة. |
| `src/Supermarket.Admin/ViewModels/SettingsViewModel.Backup.cs` | ملف جديد (C#) | كلاس جزئي يحتوي على خصائص وأوامر النسخ الاحتياطي والاستعادة والجدولة. |
| `src/Supermarket.Admin/ViewModels/SettingsViewModel.cs` | تعديل C# | حقن `IBackupService`، مع إبقاء المعامل اختيارياً أو تحديث الـ Fixture لضمان عدم كسر الاختبارات الحالية. |
| `src/Supermarket.Core/Interfaces/IBackupService.cs` | ملف جديد (C#) | واجهة تجريد عمليات النسخ والاستعادة وجدولة النسخ وإعداداتها. |
| `src/Supermarket.Core/Services/BackupService.cs` | ملف جديد (C#) | التنفيذ الفعلي لعمليات SQL Server مع تفريغ برك الاتصال ومعالجة إقفال الملفات `SINGLE_USER`. |
| `src/Supermarket.Admin/Services/AutoBackupScheduler.cs` | ملف جديد (C#) | مجدول خلفي يعمل بمؤقت زمني أثناء تشغيل تطبيق الإدارة وينفذ النسخ عند حلول موعده. |
| `tests/Supermarket.Admin.Tests/` | إضافة اختبارات | اختبارات تغطي أوامر النسخ والاستعادة، التأكيدات، معالجة الأخطاء، وحفظ إعدادات التكرار والمسار. |

---

## 8. التحقق من صحة البناء والاختبارات الحالية
- تم فحص وتأكيد نجاح بناء كافة مشاريع الحل باستخدام .NET 8 (`net8.0-windows`):
  - `Supermarket.Core` -> نجح البناء (0 أخطاء).
  - `Supermarket.Wpf` -> نجح البناء (0 أخطاء).
  - `Supermarket.Admin` -> نجح البناء (0 أخطاء).
  - `Supermarket.Admin.Tests` -> **اجتاز 34 من أصل 34 اختباراً بنجاح 100%**.
