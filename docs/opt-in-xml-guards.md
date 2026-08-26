# Opt-in защиты от malformed XML: задача и требования к реализации

**Статус:** к реализации. Код по этому документу ещё не менялся.
**Связанные документы:** [opt-in-xml-features.md](opt-in-xml-features.md), [README — Limitations](../README.md#limitations), [xmlserializer-compat.md](xmlserializer-compat.md), [perf-single-pass-parser.md](perf-single-pass-parser.md).

Документ самодостаточный: по нему должно быть можно реализовать работу, не поднимая переписку.

Это **не** та же задача, что [opt-in-xml-features.md](opt-in-xml-features.md). Фичи (`XmlFeature`) — *принимать* дополнительные XML-конструкции (CDATA, комментарии, `'` в атрибутах). Стражи (`XmlGuard`) — *отказывать* на вводе, который XML 1.0 считает не-well-formed, и который сегодня ядро молча превращает в POCO. Ортогонально: хост может быть с `CData` и без стражей, или без фич и со всеми стражами. Не складывать флаги в один enum.

---

## 1. Задача

Ядро XmlSerDe — POCO ↔ XML data binding, не универсальный XML-процессор. README честно пишет, что защиты от malformed XML на входе нет. `System.Xml.Serialization` этой защитой обладает: сначала `XmlReader` требует well-formedness, потом маппинг на объект. Люди скармливают ему тела HTTP-запросов. Compat-слой, называясь заменой `XmlSerializer`, наследует это ожидание.

Требуется:

1. **По умолчанию** native-путь (`[XmlSubject]` без дополнительных атрибутов) **не** платит за стражи: поведение на well-formed POCO-документе как сегодня, битый ввод по-прежнему может быть принят.
2. Пользователь **включает** стражи атрибутом с `[Flags]`-перечислением на классе-сериализаторе. Генератор тогда порождает код, который ловит перечисленные нарушения.
3. **Compat-слой** включает у себя **полный набор** стражей **автоматически**, без атрибута на пользовательском коде.
4. Детекция — **свой код** в `XmlScan` / декодере / сгенерированном `Deserialize`, не `XmlReader` и не предварительный проход «сначала валидатор, потом разбор».
5. Исключения — той же формы, что у `System.Xml.Serialization`, **без** строки и колонки (их у span-парсера нет).

Это не breaking change native-пути: default остаётся как сейчас. Breaking — только для compat (битый документ, который фасад сегодня принял, начнёт бросать) и для native-хоста, на который повесили атрибут.

---

## 2. Какие угрозы реально есть у POCO-биндинга

Ниже — не энциклопедия XML-атак, а то, что следует из текущего парсера и из прогона тех же документов через `System.Xml.Serialization.XmlSerializer` и через ядро (net10.0 Release, 2026-08-17).

Классические XML-атаки, которых **у этого парсера нет** и которые **не** входят в задачу:

| Атака | Почему не про POCO XmlSerDe |
|---|---|
| XXE (`<!ENTITY xxe SYSTEM "file:///…">`) | `<!DOCTYPE>` пропускается, внешние сущности не резолвятся. Нет DTD — нет XXE |
| Billion Laughs / квадратичный entity expansion | Те же пять предопределённых сущностей (`amp`/`lt`/`gt`/`apos`/`quot`) и CharRef. Вложенных general entity нет |
| Схема / XSD / SOAP | Вне скоупа ядра |
| Namespaces well-formedness (два `xmlns`, зарезервированные префиксы) | Имена сравниваются литерально; общего резолва префиксов нет |
| Неизвестные элементы | И ядро, и BCL их пропускают. Это контракт data binding, не дыра well-formedness |
| Дубли скалярных *элементов* (`<Total>1</Total><Total>9</Total>`) | Well-formed XML; оба читателя обычно берут последнее. Не страж |
| Рост `List<T>` / массива от миллиона `<Item>` | Легитимный документ. Лимит размера — политика приложения, не XML 1.0 |
| Max depth как well-formedness | `SkipBody` итеративный. Стек C# растёт только по графу *известных* типов, как у BCL |

Что **нужно** ловить — только то, что (а) XML 1.0 запрещает, (б) BCL на этом падает, (в) ядро сегодня **принимает объект**, (г) это меняет смысл POCO или доступность сервиса.

### 2.1. Несовпавший закрывающий тег сложного типа — целостность

Сгенерированный `DeserializeBody` для типа с дочерними элементами:

```
if (child.IsEmpty) break;          // конец ввода
if (child.IsEndTag) break;         // любой закрывающий тег
```

Имя закрывающего тега **не сверяется**. Тело скалярного члена (`ReadTextBody` → `EndTagLength`) сверяется; тело класса — нет.

Прогон:

```xml
<XmlObject2><StringProperty>abc</StringProperty></Wrong>
```

- BCL: `XmlException`: start tag `'XmlObject2'` does not match the end tag of `'Wrong'`.
- Ядро: **приняло** `{ StringProperty = "abc", IntProperty = 0 }`.

Вложенный вариант:

```xml
<XmlObject3>
  <XmlObjectProperty>
    <StringProperty>keep</StringProperty>
  </XmlObject3>
  <StringProperty>smuggled</StringProperty>
</XmlObjectProperty>
</XmlObject3>
```

- BCL: падает на первом несовпадении.
- Ядро: **приняло** ребёнка с `StringProperty = "keep"`. «Контрабанда» не перезаписала поле (она ушла в skip unknown и обрубилась следующим чужим `</…>`), но успех десериализации на обрезанном дереве — это уже нарушение контракта «либо объект, либо ошибка документа».

Отдельно тот же пробел даёт *принятый* обрезанный сложный тип без своего закрывающего тега:

```xml
<XmlObject2><IntProperty>1</IntProperty>
```

После последнего ребёнка `ReadHead` возвращает `EndOfInput`, цикл выходит. Скаляр без закрытия падает (`Closing tag not found`); класс — нет.

Для POCO это главная угроза целостности: атакующий обрезает документ чужим `</…>` или просто не закрывает корень, приложение получает «успешный» частично заполненный объект.

### 2.2. Второй корень и хвост после документа — целостность / контрабанда

Публичный `Deserialize` корня игнорирует `bodyConsumed` (`out _`). Всё после первого элемента не смотрится.

```xml
<XmlObject2><IntProperty>1</IntProperty></XmlObject2><XmlObject2><IntProperty>2</IntProperty></XmlObject2>
```

```xml
<XmlObject2><IntProperty>1</IntProperty></XmlObject2>not-xml
```

- BCL: `There are multiple root elements` / `Data at the root level is invalid`.
- Ядро: **приняло** первый объект (`IntProperty = 1`).

Для HTTP-тела это «первый документ победил, хвост съели». Аудитор, WAF или второй парсер могут увидеть другой документ. Для POCO достаточно правила XML 1.0: один корень, после него только `S`.

### 2.3. Повторяющиеся атрибуты — целостность (HTTP parameter pollution)

XML 1.0 запрещает два атрибута с одним qualified name. README это уже называет (`No duplicate-attribute detection`, берётся первое совпадение). Прогон:

```xml
<AttributeSubject id="1" id="2" Tag="x" kind="zero-value" flag="false" />
```

- BCL: `'id' is a duplicate attribute name`.
- Ядро: **приняло** `Id = 1` (первое значение).

Угроза ровно там, где у типа есть `[XmlAttribute]` (и `xsi:type` / `xsi:nil`: два `xsi:type` — тоже дубль). На документе без атрибутов проверки нет и платить не за что.

### 2.4. Битый синтаксис атрибута — отказ в обслуживании / неверный объект

`ParseFirstFoundAttribute` берёт `trimmed[iofa0]` / `trimmed[iofa2]` / `trimmed[iofa3]` **без** проверки `IndexOf == -1`. На well-formed голове это не стреляет; на битой:

| Вход | BCL | Ядро сегодня |
|---|---|---|
| `id=1` (без кавычек) | unexpected token, ожидались `"`/`'` | `FormatException` («`x` is not in a correct format») — разобрало не то |
| `id="1` (нет закрывающей кавычки) | unexpected token | `Closing quote not found` (через `FindUnquotedGt`) |
| `id Tag="x"` (нет `=`) | expected `=` | **приняло**, `Id = 0`, `Tag` пустой |

Это не XXE. Это либо падение с «не тем» исключением (обход `catch (InvalidOperationException)`), либо тихий неверный POCO. Лечится проверкой границ в том же методе, который и так ходит по атрибутам — **сделано**, см. §5.1: падать с bounds-исключением ядро больше не может, но «приняло не то» (первые две строки таблицы) остаётся вопросом строгости и ждёт стражей.

`docs/xmlserializer-compat.md` писал про бесконечный цикл из `IndexOf('<') == -1`. В `ReadTextBody` / `SkipBody` / `SkipToTag` это уже throw. Дыра осталась в разборе атрибутов.

### 2.5. Запрещённые Char во входном тексте — низкий приоритет, дорогой наивный скан

```xml
<XmlObject2><StringProperty>a&#x1;… нет: литеральный U+0001</StringProperty></XmlObject2>
```

- BCL: `hexadecimal value 0x01, is an invalid character`.
- Ядро: **приняло** строку длины 3 с U+0001 внутри.

Для POCO это почти никогда не эксплойт: дальше поле — `string`, не разметка. Имеет смысл только как паритет с BCL на compat. **Запрещено** реализовывать полным проходом `XmlCharGuard` по всему документу (см. §3): на REGULAR это ~5 µs, больше самого разбора.

### 2.6. Что уже ловится и дублировать не надо

Прогон, ядро уже бросает:

- обрезанный *скаляр*: `Closing tag not found`;
- несовпавший close *скаляра*: `Mismatched closing tag found for StringProperty`;
- необъявленная сущность: `Reference to undeclared entity '&nbsp;'…`.

Форму исключений на этих путях всё равно привести к §6, когда включены стражи (обёртка + `XmlException`), но новых сканеров под них не писать.

---

## 3. Сколько это стоит (замер до реализации)

Машина та же семья, что в README (Windows, net10.0, Release). Методика **не** BenchmarkDotNet: `Stopwatch`, 50k итераций после прогрева, изолированные куски кандидатов. Абсолютные µs десериализации здесь **не** переносить в README — они выше цифр BDN из-за фабрики `InfoContainer` и шума. Сравнивать **добавку** с базой README: REGULAR XmlSerDe ≈ **2.07 µs**, DEEP ≈ **2.82 µs**.

| Кандидат | Как мерили | Стоимость | Вердикт |
|---|---|---|---|
| `XmlReader.Create` + `while (Read)` как pre-pass | REGULAR целиком | **~15 µs** (≥ всего быстрого пути) | Запрещён. Именно поэтому «не через XmlReader» |
| `XmlCharGuard` по **всему** документу | REGULAR / DEEP | **~5.3 µs / ~1.4 µs** | Запрещён как алгоритм стража. Линейно по разметке, не по строковым членам |
| Второй проход `ScanHead` по документу в поисках дублей атрибутов | REGULAR / DEEP / 4-атрибутная голова | **~2.7 µs / ~5.7 µs / ~0.6 µs** | Запрещён. На DEEP атрибутов нет, а скан голов всё равно платится. Только **вплавить** в уже разобранную голову с `HasAttributes` |
| `SequenceEqual` имени закрывающего тега × N | 26 имён / 100 имён, два разных спана | **~6 нс/сравнение** (~0.16 µs на REGULAR, ~0.56 µs на DEEP в изолированном цикле с `NoInlining`) | Вплавить в уже существующую ветку `IsEndTag`: голова уже содержит `DeclaredNodeType`. Добавка в горячем пути — доли процента… низкие единицы % на DEEP |
| Скан хвоста «только `S`» | пустой leftover | **~15 нс** | Вплавить в корневой `Deserialize` после `bodyConsumed`. На пустом хвосте бесплатно |

Итого ожидаемая цена **полного набора**, если делать как в §5, а не наивными пре-пассами:

| Документ | Что реально исполняется | Оценка добавки к сегодняшнему Deserialize |
|---|---|---|
| DEEP (100 уровней, 0 атрибутов, 1 строка) | 100 `SequenceEqual` на close + пустой leftover | **низкие единицы %**, цель &lt; 5% |
| REGULAR (мало атрибутов `xsi:*`, строки есть) | close-check на сложных элементах + uniqueness на головах с атрибутами + leftover + Char на декодированных строках | **порядка 5–10%**, не 2× |
| Голова с 4 `[XmlAttribute]` | uniqueness = ещё один проход 4 коротких имён по уже найденной голове | десятки–сотни нс на такой элемент |
| Native **без** атрибута | ни одного из этих вызовов | **0** |

После реализации переснять BDN (REGULAR / DEEP, native default vs native+`SystemXmlCompatible` vs compat) и вписать в README. Цифры этого раздела — порядок величины и запрет на плохие алгоритмы, не SLA.

---

## 4. Публичный API

### 4.1. Перечисление

В `XmlSerDe.Common`, `[Flags]`, имя `XmlGuard`.

```csharp
[Flags]
public enum XmlGuard
{
    None = 0,

    /// <summary>
    /// Закрывающий тег сложного типа должен совпасть с открывающим;
    /// тело не может кончиться <c>EndOfInput</c> без этого тега.
    /// Без флага любой <c>&lt;/…&gt;</c> закрывает текущий класс, отсутствие
    /// закрытия — успех с частично заполненным объектом.
    /// Скалярные тела уже сверяют имя (этот флаг их не отключает).
    /// </summary>
    MatchingEndTags = 1 << 0,

    /// <summary>
    /// После корня допустимы только символы XML S production
    /// (#x20 \| #x9 \| #xD \| #xA). Второй элемент или мусор — ошибка.
    /// </summary>
    SingleRoot = 1 << 1,

    /// <summary>
    /// Два атрибута с одним qualified name на одной голове — ошибка
    /// (XML 1.0 WFC: Unique Att Spec). Без флага берётся первое совпадение.
    /// </summary>
    UniqueAttributes = 1 << 2,

    /// <summary>
    /// Декодированный текст элемента и значение атрибута должны состоять
    /// из Char XML 1.0 §2.2. Без флага U+0001 в строке члена принимается.
    /// Не путать с <see cref="XmlFeature.CharGuard"/>: тот — сериализация.
    /// </summary>
    IllegalChars = 1 << 3,

    /// <summary>
    /// Набор, достаточный чтобы native/compat отказывали на том же
    /// malformed вводе, что <c>System.Xml.Serialization.XmlSerializer</c>
    /// на поддерживаемом подмножестве. Это же значение автоматически
    /// получает compat-слой.
    /// </summary>
    SystemXmlCompatible =
        MatchingEndTags
        | SingleRoot
        | UniqueAttributes
        | IllegalChars,
}
```

Добавлять новые члены в этот enum в рамках задачи **нельзя** без правки этого документа.

`XmlGuard.None` и отсутствие атрибута — одно и то же.

Синтаксис атрибутов (`=`, кавычки, `IndexOf == -1`) **не** является членом enum: это всегда-включённый ремонт `ParseFirstFoundAttribute` (см. §5.1). На well-formed документе ветки не берутся. Выключать «разрешить IndexOutOfRange» незачем.

### 4.2. Атрибут

На **классе-сериализаторе**, рядом с `[XmlSubject]` / `[XmlFeatures]`, не на subject-типе.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class XmlGuardsAttribute : Attribute
{
    public XmlGuard Guards { get; }

    public XmlGuardsAttribute(XmlGuard guards)
    {
        Guards = guards;
    }
}
```

Правила — как у `[XmlFeatures]`:

- Несколько атрибутов на одном классе **объединяются через OR**.
- Атрибут не на хосте не читается и не диагностируется.
- `[XmlGuards(XmlGuard.None)]` ≡ нет атрибута.
- Не расширять `[XmlSubject]` и не класть стражи вторым аргументом в `[XmlFeatures]`.

### 4.3. Compat

Сгенерированный compat-сериализатор получает `XmlGuard.SystemXmlCompatible` **внутри генератора**. Пользовательский `[XmlSubject]`-хост в той же сборке флаги **не** наследует (то же правило «не используешь drop-in — не платишь», что в [opt-in-xml-features.md](opt-in-xml-features.md) §3.3 и [xmlserializer-compat.md](xmlserializer-compat.md)).

Два хоста в одной сборке (native без стражей и compat со стражами) — два набора методов, как уже два класса.

### 4.4. Что не является API этой задачи

- Runtime-переключатель на `Deserialize(...)`. Страж выбирается **на этапе генерации**.
- MSBuild-свойство.
- Лимит размера документа / числа элементов коллекции / глубины рекурсии POCO.
- Предварительная валидация через `XmlReader`.
- Схема, namespaces, mixed content, duplicate child elements.

---

## 5. Как детектировать (свой код, вплавленный)

### 5.0. Структура генератора: не лапша из `if`

Это **требование**, не совет. Та же планка, что [opt-in-xml-features.md](opt-in-xml-features.md) §4.5. `ClassSourceProducer` уже большой; стражи ортогональны графу типа (члены, коллекции, `xsi:type`). Если в `GenerateDeserializeMember` / `GenerateDeserializeBodyMethod` / `GenerateRootDeserializeMethod` добавить `if (_guards.HasFlag(MatchingEndTags))` — генератор станет нечитаемым, а пятый страж вставится в десяток мест и разъедется.

Два binding не сливать в один «всё про XML»: `HostFeatureBinding` — какие конструкции понимать; `HostGuardBinding` — какие нарушения ловить. Producer читает оба. Не делать `HostXmlBinding` с двенадцатью флагами.

**Запрещено**

- Размазывать `HasFlag` / `switch (guards)` по методам, которые строят граф типа. Там стражей нет.
- Копировать целые шаблоны `DeserializeBody` «со стражами» и «без». Шаблон один.
- Эмитить в `{Host}.g.cs` лестницу `if (MatchingEndTags) … else …` или комментарии «здесь если SingleRoot». Рецензент, открыв сгенерированный файл, видит либо вызов `EnsureUniqueAttributes`, либо его отсутствие; либо вызов `EnsureEndTagName`, либо голый `break` — без enum и без флагов.
- Клонировать `ReadHead` по комбинации `XmlFeature × XmlGuard` (`ReadHeadGuardedQuoted` и т.п.). Комбинаторный взрыв — та же лапша в Common.
- Второй объект binding, который producer пересчитывает в каждом `Generate*`. Собрали один раз в ctor producer — дальше только читаем поля.
- Runtime `if (guards.HasFlag)` в `XmlScan.ReadHead` «на всякий случай, JIT свернёт». Default-хост на этот код не ссылается.

**Обязательно**

`HostGuardBinding.From(XmlGuard)` — чистый тип без `Compilation` и без Roslyn. Юнит: набор флагов → ожидаемые имена методов / сниппеты. Регрессия «включили UniqueAttributes, а хост его не зовёт» ловится без `CSharpGeneratorDriver`.

Поля binding (имена — ориентир):

```
EndTagStatement          // default: break;  guard: EnsureEndTagName(...); break;
EndOfInputStatement      // default: break;  guard: ThrowUnexpectedEof();
AfterReadHead            // default: пусто;  guard: if (child.HasAttributes) EnsureUniqueAttributes(...)
RootBodyConsumed         // default: out _;  guard: out bodyConsumed + EnsureNoTrailingMarkup
DecodeElementText        // имя checked/unchecked; стык с XmlFeature.CData — 2×2 перегрузки декодера, не больше
DecodeAttributeValue     // то же для атрибутных строк
```

Все интерполяции в `ClassSourceProducer` используют `_guards.EndTagStatement`, не пересчитывают флаги. Место, куда вставляется close-check — **одно**: ветка `IsEndTag` в `GenerateDeserializeMembers`. Uniqueness — **одно**: сразу после `ReadHead` в том же цикле. SingleRoot — **одно**: корневой `Deserialize`. IllegalChars не торчит из producer, если декодер выбирается тем же binding, что уже выбирает `DecodeElementText` для CDATA; если фичи ещё не вмержены — одно поле имени декодера здесь.

Независимые оси **не** перемножаются с осями `XmlFeature`:

| Страж | Где живёт | Что видит генератор |
|---|---|---|
| always-on границы атрибута | `ParseFirstFoundAttribute` | ничего; все хосты |
| `MatchingEndTags` | два поля binding на `IsEndTag` / `IsEmpty` | готовые сниппеты, не `if` в producer |
| `SingleRoot` | только корневой `Deserialize` | поле: `out _` vs `out consumed` + вызов |
| `UniqueAttributes` | вызов после `ReadHead`, если `HasAttributes` | поле: имя метода или пусто |
| `IllegalChars` | `DecodeElementText` / `DecodeAttributeValue` | имя checked-примитива |

Стык с `CData`: четыре имени декодера (`Decode`, `DecodeCData`, `DecodeChecked`, `DecodeCDataChecked`) выбирает **композиция двух binding в одном месте** (`From(features, guards)`-helper или таблица 2×2), не вложенные `if` в `GenerateDeserializeMember`.

**Как выглядит сгенерированный `DeserializeBody`**

Как сегодня: цикл `ReadHead` → сравнение имени → `ReadTextBody` / рекурсия. Отличаются только вставленные вызовы из binding. В `.g.cs` нет `XmlGuard`, нет `HasFlag`.

**Как не структурировать**

```
// в ClassSourceProducer.GenerateDeserializeMembers — нельзя
if (_guards.HasFlag(XmlGuard.MatchingEndTags))
    emit EnsureEndTagName(...)
else
    emit break
```

```
// в том же методе — нельзя
emit "if (guards.HasMatchingEndTags) { ... } else { break; }"
```

```
// можно
emit $"{_guards.EndTagStatement}"
```

Producer по-прежнему ветвится по *типу члена* — эта ось остаётся. Стражи в неё не вплетаются.

В ревью `ClassSourceProducer`: поиск `HasFlag` / `XmlGuard.` вне ctor/присвоения binding — замечание, блокирующее приёмку. Исключение — комментарий, ссылающийся на этот раздел.

**Недостаточно для приёмки**

- «добавили `bool matchingEndTags` в `ReadHead` и генератор передаёт `true`/`false`» — runtime-флаг, default всё равно тащит ветку;
- «в `GenerateDeserializeMembers` четыре `if (HasFlag)`» — поведение может совпасть, структура — нет.

---

Принцип детекции: вплавить в уже существующий курсор, не делать пре-pass. В рантайме на default-пути **нет** `if (guards.HasFlag)`.

### 5.1. Always-on: границы в `ParseFirstFoundAttribute` — **сделано**

После каждого `IndexOf` / `IndexOfAny`: если `&lt; 0` — `throw`, не `trimmed[-1]`, не «съесть соседний атрибут».

Это чинит таблицу §2.4 на **всех** хостах. Happy path: одно сравнение с `-1`, предсказано как false. В бенчмарк default это не должно быть видно.

Не делать из этого флаг.

Сделано вместе с двумя соседними местами того же рода: в `ParseAttribute` — выход
по концу головы (`index >= internalsOfHead.Length`) и требование двигаться вперёд
(`TotalLength <= 0` → throw, иначе цикл вечный), в `XmlSerDe.Compat/XmlPrologue` —
незакрытое `<?xml` (было `Slice(-1 + 2)`, то есть разбор с середины объявления).
Исключение пока обычное `InvalidOperationException`, не форма §6: та появится
вместе с остальными стражами.

Инвариант закреплён корпусом — `XmlSerDe.Tests/MalformedInputFixture.cs`: все
префиксы, все удаления и все замены одного символа на разметочный для трёх
документов, плюс битые головы и незакрытая разметка. Требование: результат либо
`InvalidOperationException`/`FormatException`; `IndexOutOfRangeException` и
`ArgumentOutOfRangeException` — провал теста. Корпус нашёл случай, который руками
не пишется: потерянный `=` в `p3:type"…"`.

### 5.2. `MatchingEndTags`

`DeserializeBody` должен знать ожидаемое имя (имя типа / `[XmlRoot]` / `[XmlElement]` того члена, чьё тело сейчас разбирается). Сейчас в метод оно не передаётся — передать `roschar expectedEndName` с головы, которая уже прочитана.

На `child.IsEndTag`:

- default: как сейчас, любой close заканчивает тело;
- страж: `SequenceEqual(child.DeclaredNodeType, expectedEndName)` (плюс XML `S` перед `>`? **нет** — сегодня `EndTagLength` для скаляров требует `>` сразу после имени; не расширять в этой задаче). Несовпадение → §6, inner как у BCL без позиции: `The '{expected}' start tag does not match the end tag of '{actual}'.`

На `child.IsEmpty` (конец ввода):

- default: `break`, объект принят;
- страж: ошибка, inner в духе `Unexpected end of file has occurred.` Список незакрытых элементов **не обязателен** (для него нужен стек имён; это цена и сложность без выигрыша в security). Не строить стек только ради текста BCL.

Скалярный `EndTagLength` уже бросает. При включённых стражах обернуть его исключение в форму §6 (один helper `ThrowXmlDocumentError`), не писать вторую сверку имени.

### 5.3. `SingleRoot`

Корневой `Deserialize` сегодня:

```
Deserialize(…, xmlFullNode, …, out result, out _);
```

Со стражем: забрать `bodyConsumed`, взять хвост `xmlFullNode.Slice(head + bodyConsumed)`, допустить только `S`. Любой другой символ → inner `Data at the root level is invalid.` (мусор) либо `There are multiple root elements.` (хвост начинается с `<`). Различать по первому непробельному: `<` vs остальное. Позицию не считать.

Без стража по-прежнему `out _`.

Не сканировать документ с начала повторно.

### 5.4. `UniqueAttributes`

Только если `head.HasAttributes`. Не вызывать на DEEP-подобных головах без атрибутов.

Алгоритм: один проход уже существующим `ParseFirstFoundAttribute` от конца имени до конца головы. Qualified name = prefix + `:` + local (или local). Сравнение с уже увиденными: `stackalloc` срезов в `FullHead`, до ~16 имён без кучи; больше 16 на POCO не бывает, если вдруг — бросать как дубль или расти на стеке, не `List`. Дубль → inner `'id' is a duplicate attribute name.` (подставить local name, как BCL; префикс в тексте BCL обычно не пишет для `id`).

Вызов из сгенерированного кода сразу после `ReadHead`, не из второго `IndexOf('<')` по документу. Не менять «берётся первое» для default.

Генератор **не** обязан сливать этот проход с присвоением `[XmlAttribute]`-членов в один (сейчас каждый член ищет себя сам). Слияние — отдельная оптимизация, не эта задача.

### 5.5. `IllegalChars`

Только декодированные значения, которые станут `string` (тело элемента, значение атрибута). Не разметка, не `int.Parse`.

В fast-path `DecodeElementText`, где сейчас `IndexOfAny('&','<') < 0` → `ToString()`: перед этим `XmlCharGuard.EnsureValidXmlChars` в checked-перегрузке. В ветке с ссылками — проверка code point уже почти есть для CharRef; литеральный U+0001 в «сыром» куске тоже должен отсекаться.

Исключение inner: `'{c}', hexadecimal value 0xHH, is an invalid character.` без `Line N, position M`.

Default-декодер **не** зовёт guard (как default-сериализация без `XmlFeature.CharGuard`).

### 5.6. Запрещённые реализации

```
// нельзя: pre-pass
using var r = XmlReader.Create(...);
while (r.Read()) { }
Deserialize(span, out obj);
```

```
// нельзя: второй скан документа
foreach (tag in document) EnsureUniqueAttributes(tag);
```

```
// нельзя: CharGuard(xmlFullNode) на корне
```

```
// нельзя в ClassSourceProducer.GenerateDeserializeMember
if (_guards.HasFlag(XmlGuard.MatchingEndTags)) emit ...
```

---

## 6. Форма исключений

Эталон — прогон BCL, форма **без** `IXmlLineInfo` (её уже использует compat):

```
InvalidOperationException("There is an error in the XML document.")
  InnerException: XmlException(сообщение без "Line N, position M.")
```

Сообщения inner снимать с BCL, вырезая хвост ` Line {n}, position {m}.` и вхождения ` on line {n} position {m}` / ` on line {n}`. Не выдумывать новые формулировки, если BCL уже дал текст.

Зафиксированные прогоном (позиция снята):

| Ситуация | Inner `XmlException.Message` |
|---|---|
| EOF / нет закрытия | `Unexpected end of file has occurred.` (без списка элементов — см. §5.2) |
| Несовпавшие теги | `The '{start}' start tag does not match the end tag of '{end}'.` |
| Второй корень | `There are multiple root elements.` |
| Мусор после корня | `Data at the root level is invalid.` |
| Дубль атрибута | `'{name}' is a duplicate attribute name.` |
| Нет кавычки у атрибута | `'{token}' is an unexpected token. The expected token is '"' or '''.` |
| Нет `=` | `'{token}' is an unexpected token. The expected token is '='.` |
| Нелегальный Char | `'{ch}', hexadecimal value 0x{hh}, is an invalid character.` |
| Необъявленная сущность | как сейчас у декодера **или** BCL `Reference to undeclared entity '{name}'.` — при стражах лучше BCL-форма, без нашей длинной лекции про DTD |

Внешняя обёртка:

- **Compat** уже делает `DeserializationFailed`; внутренности сканера должны быть `XmlException`, чтобы цепочка совпала с BCL (`IOE` → `XmlException`), а не `IOE` → `InvalidOperationException("Closing tag not found")`.
- **Native со стражами**: та же обёртка в корневом `Deserialize` (один helper). Не оборачивать каждый примитив трижды.
- **Native без стражей**: сегодняшние голые `InvalidOperationException` с текущими текстами **сохранить**. Не заставлять default платить аллокацией `XmlException` на ошибках, которые и так ловят тесты ядра.

Не требовать совпадения `XmlException.LineNumber` / `LinePosition` (у BCL без `IXmlLineInfo` они 0). Не требовать совпадения `SourceUri`.

`FormatException` с разбора `int` на **well-formed** `<Number>abc</Number>` — это не malformed XML, стражи его не трогают. `id=1` (не well-formed) не должен доходить до `int.Parse` «чужого» токена: сначала синтаксис атрибута (§5.1).

---

## 7. Поведение compat-слоя

`ClassSourceProducer` / compat-producer вызывается с `XmlGuard.SystemXmlCompatible`. Пользователь атрибут не пишет.

Тесты:

- Все кейсы §2.1–2.5 на фасаде бросают `IOE` с сообщением без позиции, inner `XmlException`.
- Well-formed документ, который сегодня ускоряется, по-прежнему `IsAccelerated` и round-trip.
- Native-хост без `[XmlGuards]` в том же процессе **принимает** `</Wrong>` на сложном типе — доказательство изоляции.
- Подключение compat не меняет сгенерированный текст native-хоста.

Почему в `SystemXmlCompatible` входит каждый флаг:

| Флаг | Зачем BCL-interop |
|---|---|
| `MatchingEndTags` | `XmlReader` сверяет стек тегов; без этого drop-in врёт на обрезанном дереве |
| `SingleRoot` | `XmlReader` не отдаёт второй корень |
| `UniqueAttributes` | WFC Unique Att Spec |
| `IllegalChars` | `XmlReader` отвергает Char вне §2.2 |

---

## 8. Связь с `XmlFeature`

| | `XmlFeature` | `XmlGuard` |
|---|---|---|
| Вопрос | понимать ли эту конструкцию | отказать ли, если XML 1.0 это запрещает |
| Default native | выключено (после задачи фич) | выключено |
| Compat | `XmlFeature.SystemXmlCompatible` | `XmlGuard.SystemXmlCompatible` |
| Пример | без `QuotedAttributes` голова режется по первому `>` | со стражами битая голова атрибута — `XmlException`, не `FormatException` |

Реализовывать стражи можно **до, после или параллельно** с [opt-in-xml-features.md](opt-in-xml-features.md). Если фичи ещё не вмержены: стражи вешать на текущий полный сканер (он уже quote-aware). Если фичи уже есть: `UniqueAttributes` зовётся после того `ReadHead`, который выбрал feature-binding; не плодить `ReadHeadGuardedQuoted`.

Конфликт не предусматривается: выключенный `QuotedAttributes` + включённый `UniqueAttributes` — uniqueness на той голове, которую простой `>` уже отрезал. Документ с `attr="1>2"` для такого хоста и так не обещан.

---

## 9. Что менять в коде (ориентир)

- **Common:** enum, атрибут, границы в `ParseFirstFoundAttribute`, `EnsureUniqueAttributes`, `EnsureNoTrailingMarkup`, checked-декодер, helper `ThrowXmlDocumentError` / создание `XmlException`.
- **Generator:** чтение `[XmlGuards]` (OR); compat всегда полный набор; `HostGuardBinding`; сниппеты в `DeserializeBody` и корневом `Deserialize`; вызов uniqueness после `ReadHead` при `HasAttributes`.
- **Compat:** убедиться, что inner уже `XmlException`, не двойная обёртка `IOE`→`IOE`.
- **Tests / README / бенчмарки / приёмка документации:** §11–§14.

Не смешивать с рефакторингом `XmlNode2`, новыми BCL-атрибутами, лимитами размера.

`XmlNode2` (публичный, не hot path): либо те же стражи по переданному `XmlGuard`, либо остаётся «как сейчас». Тесты стражей сериализатора гоняют **сгенерированный** `Deserialize`.

---

## 10. Ограничения реализации

1. **Не XmlReader и не второй проход по документу.** См. §3 и §5.6.
2. **Не runtime-флаг в горячем цикле** на default-пути. Default не вызывает стражи.
3. **Compat не заражает native.**
4. **Не платить uniqueness на головах без атрибутов.**
5. **Не сканировать Char по разметке.**
6. **Генератор не лапша.** Подробности, запреты, обязательный binding и «недостаточно» — §5.0. Этот пункт не ослабляет §5.0.
7. **netstandard2.0 / net8 / net10.** Стражи не живут только под `#if NET8_0_OR_GREATER`.
8. **Инкрементальность:** правка `[XmlGuards]` → `Generate` не `Cached`.
9. **Список незакрытых тегов в EOF-сообщении не обязателен.**
10. **Документация и цифры BDN** — §13. Без таблицы стоимости из прогона задача не принята.

---

## 11. Тесты

Три семейства хостов:

| Семейство | Атрибут | Зачем |
|---|---|---|
| Default | нет | POCO round-trip; битый ввод §2 **принимается** там, где сегодня принимается |
| Per-guard | ровно один флаг | ловит своё; соседнее нарушение без своего флага — нет (кроме always-on синтаксиса атрибута) |
| Full / Compat | `SystemXmlCompatible` / фасад | паритет с BCL по типу и тексту без позиции |

Нельзя доказывать `MatchingEndTags` только на хосте с `SystemXmlCompatible`.

### 11.1. Поведенческие, native default (нет атрибута)

- round-trip обычного POCO — зелёный;
- `<XmlObject2>…</Wrong>` — **успех**, как сегодня (тест фиксирует принятие, чтобы страж не включили «всем»);
- второй корень — **успех**, первый объект;
- дубль `id` — **успех**, первое значение;
- `id=1` — после always-on ремонта **ошибка** (это не флаг; тест на любом хосте).

### 11.2. По одному флагу

- только `MatchingEndTags`: `</Wrong>` на классе → ошибка; второй корень всё ещё успех;
- только `SingleRoot`: второй корень / `not-xml` → ошибка; `</Wrong>` на классе всё ещё успех;
- только `UniqueAttributes`: дубль `id` → ошибка; `</Wrong>` успех;
- только `IllegalChars`: U+0001 в строке → ошибка; дубль `id` успех.

Always-on: `id=1`, `id Tag="x"` → ошибка на любом из этих хостов.

### 11.3. Полный набор / compat

Сличить с BCL на одном и том же документе: тип наружного исключения, сообщение наружного (без позиции), тип inner. Текст inner — BCL минус позиция, с оговоркой §6 про список незакрытых тегов.

Корпус минимум: все строки прогона §2 плюс well-formed контроль.

### 11.4. Сгенерированный текст

Через `GeneratorHarness`:

- хост без `[XmlGuards]`: в `{Host}.g.cs` нет `EnsureUniqueAttributes`, `EnsureNoTrailingMarkup`, checked-decode;
- хост с одним флагом: есть только его примитив;
- compat-класс содержит полный набор, в исходнике пользователя `[XmlGuards]` нет.

Юнит на `XmlGuard` → `HostGuardBinding` без Roslyn.

### 11.5. Инкрементальность

Как в [opt-in-xml-features.md](opt-in-xml-features.md) §10.4, для `[XmlGuards]`.

### 11.6. Регрессия

Существующие round-trip и `Interop/*` на well-formed документах зелёные на default. `BrokenXmlOnDeserialize_ThrowsLikeSystemXml_Test` в compat остаётся зелёным и начинает покрывать inner `XmlException`, если ещё не покрывает.

---

## 12. Бенчмарки

Цифры в пользовательской документации — **только** из `XmlSerDe.PerformanceTests` / BenchmarkDotNet, тот же прогон и те же категории, что таблица Performance в README. Оценки §3 (Stopwatch, изолированные куски, «порядка 5–10%») в README **не копировать**. После реализации §3 этого документа переписать фактом BDN или пометить «оценка до реализации, актуально: README».

`Program.Main` по-прежнему не раздувать четвёртым рантаймом. Serialize — net10; deserialize — три TFM, как сейчас.

Обязательно в прогоне:

1. **REGULAR / DEEP default native** — основная строка README, как сегодня. Always-on ремонт `ParseFirstFoundAttribute` не должен быть виден: если default уехал больше чем на шум BDN относительно предыдущего README, это регрессия, не «цена стражей».
2. **REGULAR / DEEP + `XmlGuard.SystemXmlCompatible`** на native-хосте-двойнике (тот же XML, второй partial-класс с атрибутом). Это строка «сколько стоит включить все стражи».
3. **По флагу, точечно**, если полный набор маскирует дорогой страж: как минимум `UniqueAttributes` на документе с атрибутами и `IllegalChars` на REGULAR со строками. Не обязательно 2^n комбинаций. Источник — тот же BDN или узкий fixture в `PerformanceTests`, не Stopwatch из чата.
4. Compat на том же документе не обязателен третьей строкой README, если native+`SystemXmlCompatible` генерирует тот же набор примитивов, что фасад. Если разъедется — отдельная строка.

Ожидание (не SLA в README, а стоп-кран в ревью): DEEP со всеми стражами &lt; 5% к default, REGULAR порядка 5–10%. **2× и выше — реализация сделала запрещённый полный скан**, приёмка нет. Не объяснять это «XML так дорог».

Не включать в основной прогон микробенчмарк `XmlReader` pre-pass: он уже измерен в §3 и запрещён.

Таблица стоимости в README (колонки обязательны):

| | REGULAR mean / ratio к default | DEEP mean / ratio к default | Allocated |
|---|---|---|---|
| native default | 1.00 | 1.00 | как сейчас |
| + MatchingEndTags | | | |
| + SingleRoot | | | |
| + UniqueAttributes | | | |
| + IllegalChars | | | |
| `SystemXmlCompatible` | | | |

Если одиночный флаг в шуме BDN — в ячейке «шум / &lt;X%», не выдуманные наносекунды. Полный набор — всегда число с того же прогона, что default.

---

## 13. Документация

Приёмка документации — часть задачи, не «потом допишем README». Полнота, корректность, свежие цифры — три отдельных требования.

### 13.1. Полнота

Читатель README без этого файла должен узнать:

- что default native **не** валидирует well-formedness;
- что включение — `[XmlGuards]`, список флагов человеческим языком (что ловит, когда нужен);
- пример узкого флага и пример `SystemXmlCompatible`;
- что compat включает полный набор **сам**;
- что это не `XmlReader`, не схема, unknown elements по-прежнему skip;
- always-on: битый синтаксис атрибута бросает и без атрибута (единственный breaking native);
- куда смотреть за стоимостью (таблица §12).

Обязательные правки README (места сегодняшнего текста — ориентир):

- **Limitations** — фраза «No malformed-XML input protection» заменяется, не удаляется смысл. Формулировка: default этого не делает; флаг / compat — делает. Не обещать «теперь всегда безопасно».
- Подраздел **Opt-in XML guards** рядом с Opt-in XML features (или общий «Opt-in», если фичи уже в README): таблица флагов, два примера, compat.
- **Drop-in / untrusted input** (там, где сейчас «facade does not add well-formedness validation») — фасад **добавляет** стражи; native без атрибута — нет.
- **Performance** — база по-прежнему default; абзац + таблица стоимости стражей из прогона §12.
- **Test coverage map** — новые семейства хостов §11.
- Release notes / breaking: always-on атрибуты; compat отказывает на втором корне и дубле атрибута.

`docs/xmlserializer-compat.md`: абзац про недоверенный ввод — не «надо закрыть или fallback», а статус этой задачи (после реализации — «сделано, ссылка»). Native в той же сборке не наследует стражи.

Не оставлять противоречий: README, этот файл и compat-док не должны одновременно утверждать «защиты нет» и «compat защищает».

### 13.2. Корректность

- Не писать, что ядро ловит XXE/DTD — их нет, и стражи их не добавляют.
- Не писать, что unknown element или дубль `<Total>` — ошибка; оба читателя skip / last-wins.
- Исключения: наружное сообщение — форма BCL без позиции; inner — `XmlException`; не обещать `LineNumber`.
- Стык с `XmlFeature.CharGuard`: сериализация vs вход. Не называть оба одним именем в README без пояснения.
- Примеры XML в README должны совпадать с контрактом: default-пример без стражей не должен притворяться, что `</Wrong>` бросает.
- Тесты §11 — источник правды для формулировок «принимает / бросает». Если README говорит иное — чинить README или тест, не оставлять расхождение.

### 13.3. Цифры benchmark

- В README только BDN из §12, с оговоркой про машину/SDK, как в существующей таблице Performance.
- Запрещено переносить Stopwatch-оценки §3 («~15 нс leftover», «~5 µs CharGuard по документу») в пользовательский текст как цену включённых стражей. §3 — запрет плохих алгоритмов.
- Ratio считать к **default native того же прогона**, не к старой таблице README из другого коммита.
- Если страж в шуме — так и написать. Не округлять шум до «0%» рядом с полным набором, у которого есть число.
- Allocated: стражи не должны выделять на happy path (никакого `List` имён атрибутов, никакого `XmlReader`). Если Allocated default вырос — регрессия always-on или аллоцирующий uniqueness.

### 13.4. Этот документ

После реализации:

- статус «реализовано»;
- §3: либо заменить цифрами BDN, либо явная пометка «оценка до реализации, актуально: README»;
- короткий раздел расхождений с текстом (как [perf-single-pass-parser.md](perf-single-pass-parser.md) §6). Требования не переписывать задним числом.

---

## 14. Критерии приёмки

Работа сделана, если одновременно:

1. Native-хост без `[XmlGuards]` не вызывает примитивов стражей (доказано тестом сгенерированного текста). Well-formed POCO round-trip и DEEP/REGULAR default в шуме прежнего BDN.
2. Каждый флаг из §4.1, кроме составного, имеет тест «только он — своё нарушение ловит, соседнее нет» (§11.2). Always-on синтаксис атрибута покрыт отдельно и зелёный на любом хосте.
3. `SystemXmlCompatible` на native и compat без пользовательского атрибута проходят матрицу §2 / §11.3 против BCL (тип + наружное сообщение без позиции + inner `XmlException`).
4. Compat не меняет сгенерированный текст native-хоста в той же сборке.
5. Default по-прежнему **принимает** `</Wrong>` на классе, второй корень и дубль `id` — иначе стражи включили всем.
6. README полон по §13.1, корректен по §13.2, таблица стоимости сверена с прогоном §12, а не с оценками §3.
7. Инкрементальность: смена `[XmlGuards]` сбрасывает generate; посторонний комментарий — нет.
8. Структура генератора по §5.0: таблица `XmlGuard` → `HostGuardBinding` тестируется юнитом; `ClassSourceProducer` не ветвится по стражам в `Generate*`; в `{Host}.g.cs` нет лестницы по флагам.
9. Нет `XmlReader` на fast path, нет второго скана документа, нет `CharGuard` по разметке. Allocated happy path не вырос из-за `List`/reader.

Недостаточно: «добавили bool в `ReadHead`». Недостаточно: «четыре `if (HasFlag)` в producer». Недостаточно: «README упоминает атрибут, цифр нет». Недостаточно: «бенчмарк гоняли, в README оставили оценки из чата».

---

## 15. Миграция

Native без атрибута **ничего не меняет** на well-formed вводе. На битом: синтаксис атрибута без кавычек/`=` начинает бросать (always-on). Это единственный breaking native, и только для ввода, который и так не XML.

Кто хочет отказ как у BCL:

```csharp
[XmlGuards(XmlGuard.SystemXmlCompatible)]
[XmlSubject(typeof(Order), true)]
public partial class OrderSerializer
{
}
```

Узко: только `[XmlGuards(XmlGuard.MatchingEndTags)]`, если вход свой, а режет чужой close.

Compat: после задачи HTTP-тело со вторым корнем или дублем атрибута начнёт бросать, как BCL. Это цель, не регрессия. В release notes compat — breaking для тех, кто опирался на «фасад принял битое».
