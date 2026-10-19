import "@typespec/http";

using TypeSpec.Http;

@service(#{ title: "Календарь звонков API" })
@doc("""
  API сервиса записи на звонок.
  Гость выбирает тип события и свободный слот в окне записи (настраиваемое число календарных дней, включая текущий; по умолчанию 14), владелец календаря управляет типами событий, настройками и смотрит предстоящие и прошедшие встречи.
  Регистрации и авторизации нет: административный раздел работает от имени единственного заранее заданного профиля владельца.
  Время в API передаётся в UTC (ISO 8601); сетка слотов вычисляется на сервере в часовом поясе владельца (по умолчанию Europe/Moscow).
  """)
namespace CallCalendar;

// ---------- Общие модели ----------

@doc("Тип события: вид встречи, которую владелец календаря предлагает для записи")
model EventType {
  @doc("Идентификатор типа события")
  id: int64;

  @doc("Название")
  @minLength(1)
  name: string;

  @doc("Описание")
  description: string;

  @doc("Длительность в минутах: кратна 30, минимум 30 (кратность проверяется на сервере)")
  @minValue(30)
  durationMinutes: int32;
}

@doc("Краткие данные типа события для вложения в другие модели")
model EventTypeSummary {
  @doc("Идентификатор типа события")
  id: int64;

  @doc("Название")
  name: string;
}

@doc("Свободный слот: возможное время начала записи")
model Slot {
  @doc("Начало слота (UTC). Сетка: каждые 30 минут в пределах рабочего дня владельца календаря, вне обеденного перерыва")
  startUtc: utcDateTime;

  @doc("Конец слота (UTC): начало плюс длительность типа события. Запись всегда целиком помещается в рабочий день владельца и не пересекается с обеденным перерывом")
  endUtc: utcDateTime;
}

@doc("Запись: занятый слот с контактами гостя")
model Booking {
  @doc("Идентификатор записи")
  id: int64;

  @doc("Тип события")
  eventType: EventTypeSummary;

  @doc("Начало (UTC)")
  startUtc: utcDateTime;

  @doc("Конец (UTC)")
  endUtc: utcDateTime;

  @doc("Имя гостя")
  customerName: string;

  @doc("Email гостя")
  customerEmail: string;
}

@doc("Предстоящая встреча в списке владельца календаря")
model Meeting {
  @doc("Идентификатор записи")
  id: int64;

  @doc("Тип события")
  eventType: EventTypeSummary;

  @doc("Начало (UTC)")
  startUtc: utcDateTime;

  @doc("Конец (UTC)")
  endUtc: utcDateTime;

  @doc("Имя гостя")
  customerName: string;

  @doc("Email гостя")
  customerEmail: string;
}

@doc("Настройки календаря владельца")
model CalendarSettings {
  @doc("Окно записи в календарных днях, включая текущий: в его пределах гость может выбрать слот")
  @minValue(1)
  bookingWindowDays: int32;

  @doc("Начало рабочего дня по времени владельца: с него начинается сетка слотов")
  workStart: plainTime;

  @doc("Конец рабочего дня по времени владельца: в него должна помещаться запись целиком")
  workEnd: plainTime;

  @doc("Начало обеденного перерыва по времени владельца или null, если перерыв не задан")
  lunchBreakStart: plainTime | null;

  @doc("Конец обеденного перерыва по времени владельца или null, если перерыв не задан")
  lunchBreakEnd: plainTime | null;
}

// ---------- Запросы ----------

@doc("Запрос на создание типа события")
model CreateEventTypeRequest {
  @doc("Название")
  @minLength(1)
  name: string;

  @doc("Описание")
  description: string;

  @doc("Длительность в минутах: кратна 30, минимум 30 (кратность проверяется на сервере)")
  @minValue(30)
  durationMinutes: int32;
}

@doc("Запрос на создание записи")
model CreateBookingRequest {
  @doc("Идентификатор типа события")
  eventTypeId: int64;

  @doc("Начало слота (UTC), должно лежать на сетке с шагом 30 минут в пределах окна записи, рабочего дня владельца и вне обеденного перерыва")
  startUtc: utcDateTime;

  @doc("Имя гостя")
  @minLength(1)
  customerName: string;

  @doc("Email гостя")
  @format("email")
  customerEmail: string;
}

@doc("Запрос на изменение настроек календаря")
model UpdateCalendarSettingsRequest {
  @doc("Окно записи в календарных днях, включая текущий; минимум 1")
  @minValue(1)
  bookingWindowDays: int32;

  @doc("Начало рабочего дня по времени владельца: строго раньше его конца")
  workStart: plainTime;

  @doc("Конец рабочего дня по времени владельца: строго позже его начала")
  workEnd: plainTime;

  @doc("Начало обеденного перерыва по времени владельца или null, если перерыв не задан; должен помещаться в рабочий день")
  lunchBreakStart: plainTime | null;

  @doc("Конец обеденного перерыва по времени владельца или null, если перерыв не задан; должен помещаться в рабочий день")
  lunchBreakEnd: plainTime | null;
}

// ---------- Ошибки (RFC 9457 Problem Details) ----------

@doc("Problem Details (RFC 9457)")
model ProblemDetails {
  @doc("URI типа проблемы")
  type?: string;

  @doc("Краткое описание типа проблемы")
  title?: string;

  @doc("HTTP-статус")
  status: int32;

  @doc("Описание конкретного случая")
  detail?: string;

  @doc("URI конкретного случая")
  instance?: string;
}

@error
@doc("Ошибка валидации запроса")
model ValidationProblem {
  @statusCode statusCode: 400;
  ...ProblemDetails;
}

@error
@doc("Ресурс не найден")
model NotFoundProblem {
  @statusCode statusCode: 404;
  ...ProblemDetails;
}

@error
@doc("Конфликт занятости: интервал пересекается с существующей записью")
model ConflictProblem {
  @statusCode statusCode: 409;
  ...ProblemDetails;
}

// ---------- Эндпоинты ----------

@tag("EventTypes")
@route("/api/event-types")
interface EventTypes {
  @doc("Список типов событий. Используется и каталогом гостя, и административным разделом владельца")
  @get
  list(): EventType[];

  @doc("Создать тип события (владелец календаря)")
  @post
  create(@body request: CreateEventTypeRequest):
    | {
        @statusCode statusCode: 201;
        @body eventType: EventType;
      }
    | ValidationProblem;
}

@tag("Slots")
@route("/api/slots")
interface Slots {
  @doc("""
    Свободные слоты для типа события в окне записи: число календарных дней по времени владельца, включая текущий (по умолчанию 14; прошедшие слоты сегодня недоступны).
    Слот свободен, если его интервал не пересекается с интервалами существующих записей любых типов событий и с обеденным перерывом владельца.
    """)
  @get
  list(@query eventTypeId: int64): Slot[] | NotFoundProblem;
}

@tag("Bookings")
@route("/api/bookings")
interface Bookings {
  @doc("""
    Создать запись на выбранный слот (гость).
    Идемпотентно по заголовку Idempotency-Key: повтор с тем же ключом возвращает первичный результат, дубль не создаётся.
    При пересечении интервала с существующей записью: 409.
    Интервал, выходящий за рабочий день или попадающий в обеденный перерыв владельца, отклоняется: 400.
    """)
  @post
  create(
    @header("Idempotency-Key")
    @doc("Ключ идемпотентности: UUID, генерируется клиентом на попытку записи")
    idempotencyKey: string,

    @body request: CreateBookingRequest,
  ):
    | {
        @statusCode statusCode: 201;
        @header("Location") location: string;
        @body booking: Booking;
      }
    | ValidationProblem
    | NotFoundProblem
    | ConflictProblem;
}

@tag("Meetings")
@route("/api/meetings")
interface Meetings {
  @doc("""
    Встречи всех типов событий (владелец календаря), сортировка по времени начала.
    По умолчанию возвращаются только предстоящие встречи; includePast=true добавляет прошедшие.
    """)
  @get
  list(
    @query
    @doc("Включать ли прошедшие встречи; по умолчанию false")
    includePast?: boolean,
  ): Meeting[];
}

@tag("Settings")
@route("/api/settings")
interface Settings {
  @doc("Текущие настройки календаря владельца")
  @get
  get(): CalendarSettings;

  @doc("Изменить настройки календаря (владелец календаря)")
  @put
  update(@body request: UpdateCalendarSettingsRequest):
    | {
        @statusCode statusCode: 200;
        @body settings: CalendarSettings;
      }
    | ValidationProblem;
}
