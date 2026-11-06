# تقرير التحقق من العرض العربي (RTL) والبناء

**التاريخ:** 6 نوفمبر 2026
**الفرع:** `arena/edfa1ba1-supermarketsystem`
**المشروع:** `SupermarketSystem` — تطبيقان WPF (`Supermarket.Admin` و `Supermarket.POS`) على .NET 8

---

## 1. ملخّص تنفيذي

| البند | الحالة |
|---|---|
| تصحيح العرض العربي من اليمين إلى اليسار في كل الشاشات | ✅ تم |
| توحيد الأرقام والباركود على الأرقام اللاتينية | ✅ تم |
| إصلاح صناديق الرسائل (MessageBox) المائلة لليسار | ✅ تم |
| تنظيف الكود المكرّر والميت | ✅ تم |
| تحديث الوثائق لتطابق البنية الجديدة | ✅ تم |
| **البناء والاختبارات (`dotnet build` / `dotnet test`)** | ⚠️ **لم يُنفَّذ في بيئة العمل الحالية** — لا يوجد .NET SDK على لينكس، والتطبيقات تستهدف `net8.0-windows` |
| **التشغيل الفعلي على ويندوز** | ⚠️ مطلوب من المستخدم — انظر القسم 7 |

> **مهم:** كل ما في هذا الملف من «تم» يعني «تم تعديل الكود»، وليس «تم التحقق بالتشغيل». لا يوجد في بيئة العمل
> `dotnet` ولا `msbuild` ولا `mono`، كما أن WPF لا يعمل على لينكس. لذلك يلزم تنفيذ خطوات القسم 7 على جهاز ويندوز
> للتأكيد النهائي. ما يمكن تأكيده آليًا (تم تنفيذه): تحليل جميع ملفات XAML (سليمة نحويًا)، والتحقق من أن كل
> `StaticResource` مستخدم معرّف فعلًا، وأن كل `Binding` يشير إلى عضو موجود، وأن كل معالج حدث في XAML له دالة
> في الـ code-behind.

---

## 2. الأخطاء التي كانت تمنع التشغيل (وأُصلحت)

| # | الخطأ | الأثر | الإصلاح |
|---|---|---|---|
| 1 | `new CultureInfo("UTF-8")` عند بدء التشغيل | `CultureNotFoundException` قبل ظهور أي نافذة → التطبيق يغلق فورًا | `AppCulture.CreateCulture()` يستخدم `ar-EG` مُستنسخة مع فواصل `.` و `,` |
| 2 | `MessageBoxOptions.RtlReading` بدون `RightAlign` | صناديق الرسائل تظهر بمحاذاة يسارية | `ArabicMessageBox` يمرّر `RtlReading \| RightAlign` دائمًا |
| 3 | أرقام عربية-هندية (`١٬٢٣٤٫٥٠`) | الأسعار غير مقروءة للكاشير وللطابعة | `DigitShapes.None` + `NativeDigits` لاتينية |
| 4 | `DashboardView` يربط `int` بخاصية `NullToVisibility` | لافتة النواقص لا تظهر أبدًا | أُضيفت `HasLowStock` إلى `DashboardViewModel` |
| 5 | تحويلات (Converters) مكرّرة في مشروعين بنفس أسماء الموارد | سلوك مختلف بين الإدارة والكاشير | مكتبة `Supermarket.Wpf` واحدة + `Themes/Converters.xaml` |
| 6 | `Console.Beep()` في تطبيق WinExe | استثناء عند رفض باركود (لا يوجد console) | مُغلَّف بـ `try/catch` |
| 7 | سلسلة الاتصال ثابتة على `Server=AHMEDHASSAN` | التطبيق لا يعمل على أي جهاز آخر | `Server=.\SQLEXPRESS` + `appsettings.Local.json` + متغيّرات بيئة `SUPERMARKET_` |
| 8 | ستايل `Window` ضمني لا يُطبَّق على النوافذ المشتقة | كل الشاشات تظهر LTR بلا أي خطأ ظاهر | ستايل مفتاح `AppWindowStyle` + إشارة صريحة في كل نافذة + `PinWindowStyle()` |
| 9 | `PermissionsWindow`/`InputDialog` غير موجودة في الإدارة | `DialogService.Prompt` و شاشة الصلاحيات لا تعمل | أُعيدت كتابتهما بنمط RTL |

---

## 3. استراتيجية التحويل إلى RTL (قرار التصميم)

```
App.OnStartup
   ├── ArabicLayout.Configure()
   │     ├── AppCulture.Configure()                  // ar-EG + أرقام لاتينية، على كل الخيوط
   │     └── FrameworkElement.LanguageProperty = ar-EG
   ├── ArabicLayout.PinWindowStyle(this, assembly)
   │     └── يجعل AppWindowStyle هو النمط الافتراضي لكل نوع Window  ← شبكة أمان
   └── الظهور الأول للنافذة
         └── Style="{StaticResource AppWindowStyle}"  (Controls.xaml / PosTheme.xaml)
               └── FlowDirection = RightToLeft  ← يُورَّث لكل العناصر داخل النافذة
```

**قاعدة واحدة:** `FlowDirection="RightToLeft"` مكتوب **مرة واحدة فقط** داخل ستايل `AppWindowStyle`، ولا يتكرر
في أي `UserControl`. الاستثناء الوحيد المقصود: المحتوى اللاتيني يعود إلى `LeftToRight` صراحةً.

### ⚠️ لماذا الستايل مفتاح (Keyed) وليس ضمنيًا؟

هذا خطأ شائع يبدو سليمًا في المصمّم ويعمل خطأً وقت التشغيل:

> في WPF، الستايل الضمني (بدون `x:Key`) يُطابَق بـ **النوع المطابق تمامًا** للعنصر.
> `<Style TargetType="{x:Type Window}">` في `App.xaml` **لا يُطبّق** على `LoginWindow` أو `MainWindow`
> لأنهما نوعان مشتقّان من `Window`.

لو تُرك الستايل ضمنيًا كما في النسخة الأولى، لظهرت كل الشاشات **من اليسار إلى اليمين** بلا أي رسالة خطأ.
لذلك:

1. الستايل صار `x:Key="AppWindowStyle"`.
2. كل نافذة (14 نافذة) تشير إليه صراحةً: `Style="{StaticResource AppWindowStyle}"`.
3. `ArabicLayout.PinWindowStyle()` يُسجّله كذلك كنمط افتراضي لكل نوع نافذة في التجميعة (شبكة أمان لأي
   نافذة تُضاف لاحقًا بنسيان السطر أعلاه).

نفس القاعدة تنطبق على `{StaticResource {x:Type DataGridRow}}` — وهو صحيح هنا لأن صفوف الشبكة هي فعلًا نوع
`DataGridRow` نفسه، لا نوع مشتق.

### أين يُستخدم `LeftToRight` (وهذا هو المطلوب، لا خطأ)

| الموضع | السبب |
|---|---|
| `LatinInputStyle` / `NumericInputStyle` / `LatinPasswordStyle` (الإدارة) | اسم المستخدم، كلمة المرور، الباركود، الهاتف، البريد |
| `PosLatinInputStyle` / `PosLatinPasswordStyle` (الكاشير) | شاشة الباركود، حقل PIN، المبالغ |
| `PermissionsWindow` — عمود مفتاح الصلاحية | المفتاح لاتيني (`products.view` …) |
| `ShiftCloseWindow` — معاينة الإيصال (Consolas) | المعاينة تحاكي الورق المطبوع سطرًا بسطر |
| `SalesAnalyticsView` — أرقام الفواتير | `RtlIsolation` + `LeftToRight` تسلسل |

### عزل النصوص اللاتينية داخل جملة عربية

`AppCulture.Isolate()` يحيط القيمة بعلامتي LRM؛ مكشوف في XAML باسم `RtlIsolation`:

| الحقل | قبل | بعد |
|---|---|---|
| رقم فاتورة `INV-2026-0007` | قد يُعرض `0007-2026-INV` | يُعرض كما هو |
| باركود `6221031...` | تنعكس أجزاؤه | سليم |
| مسار ملف CSV في رسالة | ينعكس | سليم |

---

## 4. حالة كل شاشة

### تطبيق الإدارة (Admin)

| الشاشة | RTL | الأرقام | التلميحات (ToolTip) | الشبكات (DataGrid) |
|---|---|---|---|---|
| `LoginWindow` | ✅ | لاتينية | ✅ | — |
| `MainWindow` (الشريط الجانبي يمينًا) | ✅ | — | ✅ | — |
| `DashboardView` | ✅ | `N2`/`N0` | ✅ | ✅ رؤوس عربية |
| `ProductsView` | ✅ | لاتينية | ✅ | ✅ + ترقيم صفحات |
| `LowStockView` | ✅ | لاتينية | ✅ | ✅ |
| `SuppliersView` | ✅ | هاتف LTR | ✅ | ✅ |
| `CatalogView` (أقسام + وحدات) | ✅ | `AllowFractions` بولياني | ✅ | ✅ |
| `StockMovementsView` | ✅ | `N2`/`N3` | ✅ | ✅ |
| `SalesAnalyticsView` (LiveCharts) | ✅ | محاور لاتينية | ✅ | ✅ + نافذة تفاصيل فاتورة |
| `UsersView` | ✅ | لاتينية | ✅ | ✅ |
| `PermissionsWindow` | ✅ | — | ✅ | قائمة مخصّصة |
| `SettingsView` | ✅ | نسب/أسعار | ✅ | — |
| `ProductEditWindow` / `SupplierEditWindow` / `UserEditWindow` / `StockAdjustmentWindow` | ✅ | حقول رقمية LTR | ✅ | — |
| `InputDialog` | ✅ | لاتينية | ✅ | — |

### نقطة البيع (POS)

| الشاشة | RTL | ملاحظات |
|---|---|---|
| `LoginWindow` (PIN) | ✅ | PIN يُكتب LTR، التركيز تلقائي |
| `SalesWindow` | ✅ | لوحة الأصناف يسارًا، الإجماليات يمينًا، اختصارات F1–F10 |
| `PaymentWindow` | ✅ | لوحة أرقام، مبالغ سريعة، حقول عميل آجل |
| `ShiftCloseWindow` | ✅ | تقرير X/Z، فرق النقدية ملوّن |
| `HeldCartsWindow` | ✅ | استكمال/حذف سلة معلّقة |
| `InputDialog` | ✅ | يُستخدم في إدخال الكمية والخصم |

---

## 5. تنظيف الكود

| ما تم حذفه | السبب |
|---|---|
| `Core/Localization/AppStrings.cs` (146 سطرًا) | لا يوجد أي مرجع له؛ النصوص موجودة في XAML |
| `Admin/Converters/ValueConverters.cs` + `POS/Converters/PosConverters.cs` | نسختان من نفس التحويلات → مكتبة `Supermarket.Wpf` |
| `Controls.xaml` في جذر المستودع | ملف مبتور (11 سطرًا) لا يدخل في أي مشروع |
| `MainWindow.xaml.cs::Button_Click`, `ShowComingSoon` | كود «قريبًا» غير مستخدم |
| `ShiftCloseWindow.xaml.cs::OnClosed` الفارغ | لا يفعل شيئًا |
| `WpfVisuals`, `NumericTextValidationRule`, `FormatMoney/Number/Price`, `PosErrorTextStyle`, `IsConfigured`, `FlowDirectionResourceKey` | بلا أي مستهلك |
| هوامش سالبة (`Margin="0,0,0,-12"`) في تذييلات الشاشات | استُبدلت بحدود (Border) عادية |
| `FontFamily="Tahoma"` موزّعة في 6 ملفات | موحّدة في ستايل `Window` |
| `FlowDirection="RightToLeft"` مكرّر في 5 ملفات | موحّد في ستايل `Window` |

**إضافات:** مكتبة `Supermarket.Wpf` (تحويلات + `ArabicLayout` + `ArabicMessageBox`)، و`appsettings.Local.json`
في `.gitignore`، وتحقّق تكرار للاشتراكات في الأحداث عند إغلاق النوافذ (تفادي تسريب الذاكرة).

---

## 6. توافق الاختبارات

`tests/Supermarket.Admin.Tests` تحتاج ويندوز (`net8.0-windows`). تم تحديث الاختبارات لتوقيعات البناء الجديدة:

| الملف | التعديل |
|---|---|
| `AuthAndSessionTests.cs` | `LoginViewModel` صار 5 وسائط (أُضيف `IDialogService` و `AuthSession`) |
| `MainViewModelTests.cs` | `Initialize()` → `await InitializeAsync()` (أصبحت غير متزامنة) |

لم تُحذف أي حالة اختبار ولم يُخفَّف أي تحقّق. اختبارات `Supermarket.Core.Tests` (87 اختبارًا) لم تتأثر
لأنها لا تعتمد على WPF.

### كيف تُشغَّل

```powershell
dotnet restore
dotnet build SupermarketSystem.sln -c Release
dotnet test  tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj
dotnet test  tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj   # يحتاج ويندوز
```

---

## 7. خطوات التحقق المطلوبة على ويندوز (قائمة فحص)

> نفّذها بالترتيب؛ كل بند يقابل خطأ محتملًا محددًا.

### 7.1 البناء
1. `dotnet build SupermarketSystem.sln -c Release` → يجب أن ينتهي بـ **0 أخطاء**.
2. تأكد أن `obj/` نظيف إن ظهرت أخطاء XAML قديمة: `git clean -xdf` ثم أعد البناء.

### 7.2 الإعداد
3. أنشئ `src/Supermarket.Admin/appsettings.Local.json` و `src/Supermarket.POS/appsettings.Local.json`:
   ```json
   { "ConnectionStrings": { "SupermarketDb": "Server=YOUR-PC\\SQLEXPRESS;Database=SupermarketDb;Trusted_Connection=True;TrustServerCertificate=True" } }
   ```
   (أو استخدم المتغيّر `SUPERMARKET_ConnectionStrings__SupermarketDb`.)
4. طبّق قاعدة البيانات من `docs/DATABASE-SETUP.md`.
5. شغّل `Supermarket.Admin` ودخل بـ `admin / Admin@123`.

### 7.3 شاشات الإدارة
6. **الاتجاه:** الشريط الجانبي على **اليمين**، عنوان الصفحة إلى اليمين، والأيقونات غير معكوسة.
7. **لوحة التحكم:** لافتة النواقص تظهر عند وجود منتج تحت الحد الأدنى (كانت لا تظهر).
8. **المنتجات:** ابحث بالعربية، وغيّر الترتيب من رؤوس الأعمدة، وجرّب ترقيم الصفحات.
9. **الباركود:** في نافذة إضافة منتج، اكتب/امسح باركود → يظهر من اليسار لليمين بترتيب صحيح.
10. **شبكة البيانات:** رؤوس الأعمدة عربية، الصفوف تتلوّن عند المرور، ولا أعمدة مقطوعة.
11. **صناديق الرسائل:** احذف موردًا/منتجًا → الرسالة تظهر بمحاذاة **يمين** وأزرارها بالعربية.
12. **الصلاحيات:** افتح «المستخدمون والصلاحيات» → «الصلاحيات» → المفتاح اللاتيني يقرأ صحيحًا، والشارة تصبح
    «مخصص» عند التغيير.
13. **الإعدادات:** حفظ اسم المتجر ثم أعد التشغيل → الاسم يظهر في الشريط الجانبي.
14. **التحليلات:** غيّر الفترة، وافتح فاتورة بالنقر المزدوج، وصدّر CSV.

### 7.4 نقطة البيع
15. دخل بـ PIN الكاشير → شاشة البيع تفتح على اليمين شاشة الإجماليات.
16. امسح باركود → يظهر في السلة، والإجمالي يتحدث.
17. `F3` كمية، `F4` خصم سطر، `F9` دفع → نافذة الدفع، لوحة الأرقام تعمل، «المتبقي» صحيح.
18. أكمل بيعًا نقديًا → تُطبع فاتورة 80 مم، والحروف العربية متصلة غير مقطوعة.
19. `F10` → تقفيل الورديّة: التقرير بمحاذاة يمين، وفرق النقدية يظهر بزيادة/عجز.
20. `Shift+F5` → قائمة السلات المعلقة، استكمال وحذف.

### 7.5 التحقق من الأرقام
21. أي مبلغ يظهر بالشكل `1,234.50 ج.م` (أرقام لاتينية، نقطة عشرية، فاصلة آلاف) — لا `١٬٢٣٤٫٥٠`.

---

## 8. ما لم يُغطَّ (معروف ومقصود)

* **تعدد اللغات:** التطبيق عربي فقط؛ لا يوجد تبديل لغة وقت التشغيل.
* **التواريخ الهجرية:** غير مطلوبة؛ التقويم ميلادي بأرقام لاتينية.
* **التقارير المصوّرة (LiveCharts):** النصوص العربية داخل المخططات تعتمد على خط Skia؛ تم ضبطها على خط
  النظام الافتراضي، ويجب تأكيدها بصريًا في البند 14.
* **الطابعة:** يُختبر الاتصال بالطابعة الحرارية فعليًا في البند 18؛ في حال عدم وجود طابعة يعرض البرنامج
  معاينة الإيصال على الشاشة.
