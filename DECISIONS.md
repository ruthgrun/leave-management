<div dir="rtl">

# DECISIONS

## 1. החלטות ארכיטקטוניות
- **Controller → Service → DbContext.** הלוגיקה (יתרה, אישור, validation עסקי) עברה ל-`LeaveRequestService`. ה-Controller רק ממפה תוצאה ל-HTTP.
- **בלי Repository:** `DbContext` הוא כבר Unit of Work + Repository; שכבה נוספת מעליו רק הייתה עוטפת אותו. גם בלי interface ל-service — יש מימוש אחד, והטסטים רצים מול DB אמיתי ולא mock.
- **`ServiceResult<T>` במקום exceptions** לשגיאות צפויות (NotFound / Conflict / Validation). השגיאות מוחזרות כ-`ProblemDetails` (RFC 7807), כך שה-frontend יכול להציג את `detail`.
- **DTOs לתשובה** (`LeaveRequestDto`) במקום להחזיר entities — לא חושפים את מבנה ה-DB, ואין יותר מעגל `LeaveRequest ↔ Employee` (הוסר `ReferenceHandler.IgnoreCycles`).
- **Validation של קלט** ב-DataAnnotations על ה-DTO (`[ApiController]` מחזיר 400 אוטומטית): ערך enum לא חוקי, תאריכים חסרים. כללים עסקיים (טווח תאריכים, יתרה) — ב-service.
- כל הפעולות מול ה-DB async.

**Frontend**
- `LeaveRequestsService` עוטף את ה-HTTP; ה-URL של ה-API ב-`InjectionToken` ולא hardcoded בקומפוננטה.
- טיפוסים ו-enums (`LeaveType`, `LeaveStatus`) במקום `any` ומספרי קסם.
- state עם signals; כל subscribe עם `takeUntilDestroyed` — אין memory leaks. לא הוספתי state management (NgRx וכו') — לא מוצדק למסך אחד.
- הטופס בקומפוננטה נפרדת (`LeaveRequestFormComponent`) שמשדרת `created`, והרשימה מוסיפה את הבקשה בלי טעינה מחדש. Reactive Forms עם validator ברמת ה-group לטווח התאריכים.
- אישור: loading לכל שורה, בהצלחה מוחלפת רק השורה. ב-409 מוצגת הודעה והרשימה מסונכרנת מול השרת — במקרה הזה המידע אצלנו באמת לא עדכני, ולכן רענון מכוון ולא "עיוור".

## 2. הבאג ביתרת החופשה
- **מה היה הבאג, איפה, ואיך תיקנתי:** ב-`LeaveRequestsController.Create` חושב `used` אבל לא נעשה בו שימוש — הבדיקה הייתה `days > AnnualQuota`. תוקן ל-`used + days > AnnualQuota`, ובנוסף:
  - נספרים רק ימים מאותה שנה קלנדרית.
  - בקשות Pending נספרות ביתרה, כדי שכמה בקשות ממתינות לא יחרגו ביחד.
  - תאריך סיום לפני התחלה נדחה (יצר `Days` שלילי שהגדיל את היתרה).
  - מגבלה: בקשה שחוצה שנה נזקפת לשנת ההתחלה.
- **הטסט שמוכיח את התיקון:** `Create_ExceedingRemainingQuota_IsRejected` (18/20 + בקשה ל-3 → 400). בנוסף טסטים למקרה גבול, Pending, שנה קודמת וטווח הפוך.

## 3. אישור בקשה (approve) ו‑concurrency
- **איך טיפלתי במצבים לא חוקיים (כבר אושר / לא קיים):** בקשה לא קיימת → 404. בקשה שכבר אושרה/נדחתה → 409 Conflict. בהצלחה מוחזרת הבקשה המעודכנת, כדי שה-UI יעדכן את השורה בלי לטעון את כל הרשימה.
- **מה לגבי אישור של שתי בקשות במקביל:**
  - האישור הוא `UPDATE ... WHERE Id = @id AND Status = Pending` אחד (`ExecuteUpdate`), ולא "קרא → בדוק → שמור". ה-DB מבטיח שרק אחד משני אישורים מקבילים יעדכן שורה; השני מקבל 0 שורות → 409. עובד גם ב-SQLite וגם ב-PostgreSQL.
  - עבדתי על SQLite (נעילה של כל הקובץ, אין נעילת שורות). מול PostgreSQL הייתי מוסיף טרנזקציה עם `SELECT ... FROM "Employees" WHERE "Id" = @id FOR UPDATE` לפני בדיקת היתרה, ביצירה ובאישור. זה מסדר בתור את כל הפעולות של אותו עובד, וסוגר מצב שבו שתי בקשות שנוצרות במקביל עוברות שתיהן את בדיקת היתרה.

## 4. על מה ויתרתי בגלל הזמן
- **Authentication/Authorization** — מי מאשר ומי מגיש (ראו סעיף אבטחה).
- **נעילות אמיתיות ב-PostgreSQL** (`FOR UPDATE` על העובד) + טסט concurrency אמיתי עם שני DbContext במקביל מול Testcontainers.
- **Migrations** במקום `EnsureCreated`.
- **בקשה שחוצה שנה** — פיצול הימים לפי שנה.
- **ימי עבודה** — כרגע נספרים ימים קלנדריים (כולל סופ"ש וחגים), כמו ב-POC.
- **דחיית בקשה** (reject) וביטול.
- **טסטים ל-frontend** — יש טסט ל-validator; הייתי מוסיף טסטים לקומפוננטות עם `HttpTestingController` (אישור מוצלח / 409 / 404).
- **`EmployeesController`** עדיין עובד ישירות מול ה-DbContext — קריאה פשוטה בלי לוגיקה, לא הצדיק service.

## 5. שימוש ב‑AI
עבדתי עם Claude Code (סוכן שרץ על הריפו המקומי, קורא קבצים, מריץ טסטים ועושה שינויים). עבדתי סעיף-סעיף: הוא מציע ומממש, אני עוברת על ה-diff, מבקשת שינויים, ורק אז קומיט.

### איפה AI עזר (כולל prompts)
1. **מיפוי הבעיות לפני כתיבת קוד.** prompt: הדבקתי את דף המשימה ושאלתי *"תוכל לעדכן אותי איפה לעשות את השינויים ומה לעשות?"*.
   קיבלתי מפה לפי קבצים: הבאג (`used` מחושב ולא בשימוש), ה-SQL Injection ב-`Search`, ושגם בזמן אישור צריך לחשוב על יתרה. השתמשתי בזה כדי לקבוע סדר עבודה וסדר קומיטים.
2. **תיקון הבאג + טסטים.** 
   חוץ מהתיקון עצמו הוא מצא עוד שני מקרים באותה בדיקה (בלי סינון לפי שנה, וטווח תאריכים הפוך שיוצר ימים שלילים). ביקשתי לוודא שהטסטים באמת תופסים את הבאג — הוא החזיר זמנית את הקוד הישן והראה ש-3 טסטים נכשלים עליו.
3. **בעיות סביבה.** prompt: 
   הוא אבחן שה-build עובר אבל חסר ASP.NET Core Runtime 8 (מותקן רק ה-runtime הבסיסי), ושהפקודה שנתן קודם לא עובדת ב-PowerShell 5.1.

### איפה דחיתי/תיקנתי הצעה של AI
- **פקודות bash בסביבת Windows:** הוא נתן `cd leave-management && git init && git add . && ...` — `&&` לא קיים ב-Windows PowerShell 5.1, וגם לא הייתי בתיקייה הנכונה. מעבר לזה, `git init` לא היה נחוץ בכלל — הריפו כבר היה קיים עם קומיט ראשון. הוא קבע "התיקייה עדיין לא ריפו" בלי לבדוק בתיקייה הנכונה. מכאן ביקשתי פקודות PowerShell בלבד, ושיבדוק לפני שהוא קובע.
- **אבחון שגוי של גרסת Node:** הוא קבע שמותקן Node 10 ושצריך להתקין 20 לפני שאפשר לעבוד על ה-frontend. בדיקה חוזרת הראתה ש-Node 24 מותקן ו-`node_modules` כבר קיים — לא היה צורך בהתקנה, וה-build וה-טסטים רצו כמו שהם.
- **יותר מדי הערות בקוד:** הגרסה הראשונה של התיקון כללה הערות ארוכות שמסבירות כל שורה, וגם בטסטים (Arrange/Act/Assert עם הסברים). ביקשתי לקצר להערה אחת שמסבירה את ה"למה" (למה Pending נספר) — השאר ברור מהקוד.

### אבטחה
- **SQL Injection** ב-`GET /api/leave-requests/search` (`LeaveRequestsController.Search`): הפרמטר `name` שורשר ישירות לתוך SQL ב-`FromSqlRaw`. קלט כמו `%') OR 1=1 --` משנה את השאילתה, ובמקרה גרוע אפשר להריץ פקודות על ה-DB. תוקן לשאילתת LINQ (`Contains`) — EF שולח את הערך כפרמטר.
- **אין Authentication/Authorization:** כל אחד יכול לאשר כל בקשה, כולל של עצמו. לא מימשתי (מחוץ להיקף) — הייתי מוסיף JWT ו-policy שרק מנהל יכול לאשר, ולא את הבקשה של עצמו.

## 6. הוראות הרצה
- עבדתי במצב **SQLite** (אין Docker במחשב). ב-PowerShell:
  - `$env:Database__Provider="Sqlite"; dotnet test`
  - `$env:Database__Provider="Sqlite"; dotnet run --project src/LeaveManagement.Api`
- Frontend: `npm start`, טסטים: `npm test`.

</div>
