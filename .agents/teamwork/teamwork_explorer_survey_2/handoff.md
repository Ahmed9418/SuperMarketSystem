# تقرير التسليم النهائي (Handoff Report) — المستكشف 2 (Survey 2)
# UI Layout, Admin Views, Settings View & RTL Arabic Localization

**تاريخ التقرير:** 2026-11-09T13:48:00Z  
**الوكيل:** `teamwork_explorer_survey_2`  
**المرسل إليه:** `ed585109-e633-4496-b0a3-a5993f8ab64b` (Orchestrator)  
**نوع التسليم:** Hard Handoff (اكتملت مهمة المسح والتحليل بنجاح تام)

---

## 1. الملاحظات المباشرة (Observation)

1. **هيكل شاشة الإعدادات الحالية:**
   - المسار: `src/Supermarket.Admin/Views/SettingsView.xaml` (إجمالي 634 سطراً).
   - في السطور 11–18: السطح الرئيسي يبدأ بـ `<Grid>` بصفين؛ الصف الأول (`Row="0"`) يحتوي على `<ScrollViewer>` واحد يضم بطاقات بيانات المتجر (الأسطر 26–118) والطباعة والضريبة (الأسطر 121–175)، يليه قسم معاينة الفاتورة الحي (الأسطر 179–590)، ثم بطاقة معلومات النظام (الأسطر 592–604).
   - في السطور 609–631: الصف الثاني (`Row="1"`) يضم شريط الأزرار السفلي (`StatusMessage`، `TogglePreviewCommand`، `SaveCommand`، `LoadCommand`).
   - لا يوجد حالياً `TabControl` داخل `SettingsView.xaml`.

2. **نمط التبويبات المتوفر في النظام:**
   - المسار: `src/Supermarket.Admin/Themes/Controls.xaml` (السطور 324–335).
   - تم التحقق من وجود نمط صريح لـ `TabControl` و `TabItem`:
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
   - المسار: `src/Supermarket.Admin/Views/CatalogView.xaml` (السطور 37–163).
     يستخدم بالفعل `<TabControl Grid.Row="1">` للفصل بين تبويب «الأقسام» وتبويب «وحدات القياس».

3. **أنماط البطاقات والفواصل وعناصر التحكم:**
   - المسار: `src/Supermarket.Admin/Themes/Controls.xaml`
     - البطاقات: `Style="{StaticResource CardBorderStyle}"` (السطور 309–316) تستخدم `Background="{StaticResource SurfaceBrush}"`، `CornerRadius="{StaticResource CardCornerRadius}"` (10)، و `Padding="{StaticResource CardPadding}"` (16).
     - عناوين البطاقات: `Style="{StaticResource CardTitleTextStyle}"` (السطور 338–343).
     - تسميات الحقول: `Style="{StaticResource FieldLabelTextStyle}"` (السطور 345–349).
     - الأزرار: `PrimaryButtonStyle` (أزرق)، `SecondaryButtonStyle` (أزرق فاتح)، `DangerButtonStyle` (أحمر).
     - لا توجد مكتبات خارجية (مثل ModernWpf أو MaterialDesignInXAML). لا يوجد حالياً ستايل مخصص لـ `ToggleSwitch`، ولكن يمكن بناؤه كقالب `ControlTemplate` لـ `CheckBox`.

4. **معايير العرض العربي (RTL) والأرقام ومربعات الحوار:**
   - المسار: `src/Supermarket.Wpf/ArabicLayout.cs` و `docs/RTL-VERIFICATION.md` (السطور 47–80).
     - اتجاه `FlowDirection = RightToLeft` يُطبّق مرة واحدة مركزياً في `AppWindowStyle` على مستوى النافذة وتورثه جميع الواجهات.
     - الأرقام لاتينية قياسية دائماً في مصر (`AppCulture.CreateCulture` يستخدم `DigitShapes.None`).
     - النصوص ومسارات الملفات اللاتينية (`C:\...`) تُعزل باستخدام `LatinInputStyle` (`FlowDirection="LeftToRight"`).
   - المسار: `src/Supermarket.Admin/Services/DialogService.cs` و `src/Supermarket.Wpf/ArabicMessageBox.cs`.
     - `DialogService` يعتمد على `ArabicMessageBox` الذي يمرر إلزامياً:
       `MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign`
     - دالة `Confirm(message, title)` تستخدم `MessageBoxButton.YesNo` ويكون الزر الافتراضي هو `MessageBoxResult.No`.
     - نافذة اختيار الملفات الحالية تستخدم `Microsoft.Win32.OpenFileDialog` مباشرة في `SettingsViewModel.cs` (السطر 168).

5. **فحص البناء والاختبارات الحالية:**
   - أمر الفحص: `dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj`
   - النتيجة:
     `Passed! - Failed: 0, Passed: 34, Skipped: 0, Total: 34, Duration: 4 s - Supermarket.Admin.Tests.dll (net8.0)`

---

## 2. سلسلة الاستنتاج المنطقي (Logic Chain)

1. **انطلاقاً من الملاحظة (1) والملاحظة (2):**
   حيث أن شاشة الإعدادات الحالية `SettingsView.xaml` عبارة عن صفحة أحادية بدون تبويبات، وحيث أن التطبيق يحتوي بالفعل على نمط تبويبات قياسي ومطبّق بنجاح في `CatalogView.xaml`، فإن أنسب وأضمن طريقة لتحقيق المتطلب R2 هي تحويل حاوية الصف الأول في `SettingsView.xaml` إلى `TabControl` يضم:
   - تبويب 1: «⚙ الإعدادات العامة والطباعة» (General Settings & Printing) يحوي المحتوى الحالي.
   - تبويب 2: «💾 النسخ الاحتياطي واستعادة البيانات» (Database Backup & Recovery) للوظائف الجديدة.

2. **انطلاقاً من الملاحظة (3):**
   نظراً لعدم وجود مكتبة أطراف ثالثة لـ `ToggleSwitch`، فإن استيراد مكتبات خارجية ثقيلة (كـ ModernWpf أو MaterialDesign) قد يؤدي إلى تعارضات في الأنماط أو مشاكل في تجميع حل .NET 8. لذلك، فإن الحل الأمثل والأكثر استقراراً وتوافقاً مع بنية المشروع هو تعريف `ToggleSwitchStyle` لعنصر `CheckBox` في `Themes/Controls.xaml` بقالب `ControlTemplate` انزلاقي حديث ثنائي الحالة (Track & Thumb) مع ضبط `FlowDirection="LeftToRight"` على مسار الانزلاق الصغير لضمان سلاسة حركة المفتاح، بينما تبقى التسمية النصية باللغة العربية.

3. **انطلاقاً من الملاحظة (4):**
   مسارات الملفات مثل مسار حفظ النسخة ومسار مجلد النسخ التلقائي يجب أن تستخدم `Style="{StaticResource LatinInputStyle}"`، وذلك لمنع انقلاب الخطوط المائلة (`\`) وحروف محركات الأقراص (`C:\`) التي تحدث عند عرض نصوص إنجليزية داخل حاوية عربية بدون عزل LTR.

4. **انطلاقاً من الملاحظة (4) والمتطلب R2 و R4:**
   أمر استعادة قاعدة البيانات يجب أن يستدعي دالة `_dialogs.Confirm(...)` التي توفر بطبيعتها مربع رسالة مؤمن بمحاذاة عربية صحيحة وزر رفض افتراضي، بما يمنع أي استعادة عرضية غير مقصودة.

5. **انطلاقاً من الملاحظة (1) ونمط MVVM:**
   للحفاظ على حجم ملفات سليم وتجنب كسر 34 اختباراً قائماً، فإن استخدام الكلاس الجزئي `SettingsViewModel.Backup.cs` يوفر الخصائص والأوامر الخاصة بالنسخ والاستعادة (`BackupNowCommand`, `RestoreDatabaseCommand`, `IsAutoBackupEnabled`, إلخ) بشكل نظيف مع إتاحة المعامل `IBackupService` اختيارياً في المشيد أو عبر DI.

---

## 3. التحفظات والافتراضات (Caveats)

1. **إصدار بيئة التشغيل:** .NET SDK المثبت على بيئة التطوير هو `10.0.403`، والمشاريع تستهدف `net8.0-windows`. عملية بناء WPF تتطلب تفعيل `BypassSandbox: true` في سياق الأوامر الموجهة للأداة لضمان استعادة الحزم بشكل سليم.
2. **اختيار المجلدات:** تم التحقق من أن .NET 8 في WPF يوفر `Microsoft.Win32.OpenFolderDialog` قياسياً لاختيار المجلدات؛ في حال تشغيل بيئات أقدم يمكن استخدام حوار استعراض بديل بأمان.
3. **لا تعديل على الكود:** هذه المهمة هي مهمة استكشاف ومسح فقط (Read-only Survey)؛ لم يتم تعديل أي ملف في كود المشروع، وتم إيداع كافة التحليلات والمواصفات في مجلد الوكيل الحالي.

---

## 4. الخاتمة وخطة التكامل (Conclusion)

1. **نقطة التكامل الدقيقة:**
   - ملف الواجهة: `src/Supermarket.Admin/Views/SettingsView.xaml` (استبدال `ScrollViewer` المباشر بـ `TabControl` يحوي التبويبين).
   - ملف الأنماط: `src/Supermarket.Admin/Themes/Controls.xaml` (إضافة ستايل `ToggleSwitchStyle`).
   - ملف النموذج: كلاس جزئي جديد `src/Supermarket.Admin/ViewModels/SettingsViewModel.Backup.cs` متصل مع `SettingsViewModel.cs`.
2. **تصميم التبويب الثاني (3 بطاقات متناسقة):**
   - **بطاقة الحالة (Status Card):** نوع الخادم (`SQL Server`)، اسم القاعدة (`SupermarketDb`)، تاريخ ووقت آخر نسخة، ورسالة الحالة مع مؤشر تقدم غير محدد أثناء العمليات.
   - **بطاقة النسخ والاستعادة اليدوية (Manual Card):**
     - زر بارز `PrimaryButtonStyle`: «💾 إنشاء نسخة احتياطية الآن» يفتح `SaveFileDialog`.
     - زر تحذيري `DangerButtonStyle`: «⚠️ استعادة قاعدة البيانات» يفتح `OpenFileDialog` ويطلب تأكيداً عبر `Confirm`.
   - **بطاقة النسخ التلقائي المجدول (Auto-Backup Card):**
     - عنصر `CheckBox` بنمط `ToggleSwitchStyle` بعنوان «التسجيل الاحتياطي التلقائي».
     - قائمة `ComboBox` لتكرار النسخ («يومي» و «أسبوعي»).
     - حقل نصي LTR لمسار المجلد مع زر استعراض `SecondaryButtonStyle`.
     - زر «💾 حفظ إعدادات النسخ التلقائي».

---

## 5. طريقة التحقق المستقل (Verification Method)

يمكن للوكيل المنفذ والمراجع التحقق المستقل من صحة نتائج هذا المسح عبر الخطوات التالية:

1. **التحقق من ملفات الأنماط والتبويبات القائمة:**
   - فحص `src/Supermarket.Admin/Themes/Controls.xaml` عند السطر 324 لتأكيد وجود ستايل `TabControl`.
   - فحص `src/Supermarket.Admin/Views/CatalogView.xaml` عند السطر 37 لتأكيد نجاح استخدام نمط التبويبات ذاته.
2. **التحقق من مربعات الرسائل و RTL:**
   - فحص `src/Supermarket.Wpf/ArabicMessageBox.cs` عند السطر 12 للتأكد من خيارات `RtlReading | RightAlign`.
   - فحص `src/Supermarket.Admin/Services/DialogService.cs` للتأكد من توافر دالة `Confirm`.
3. **التحقق من اكتمال البناء واجتياز الاختبارات:**
   تشغيل أمر الاختبارات للتأكد من أن بيئة التطوير سليمة تماماً:
   ```powershell
   dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj
   ```
   *شرط النجاح:* اجتياز جميع الـ 34 اختباراً بنسبة 100% بدون أي إخفاق.
