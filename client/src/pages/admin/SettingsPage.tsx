import { Button, Container, NumberInput, Skeleton, Stack, Text, Title } from '@mantine/core'
import { TimeInput } from '@mantine/dates'
import { notifications } from '@mantine/notifications'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { getSettingsGetQueryKey, settingsUpdate, useSettingsGet } from '../../api/gen'
import { problemFieldErrors, type FieldErrors } from '../../api/problemErrors'
import type { UpdateCalendarSettingsRequest } from '../../api/gen/model'

type SettingsFieldErrors = FieldErrors<
  'bookingWindowDays' | 'workStart' | 'workEnd' | 'lunchBreakStart' | 'lunchBreakEnd'
>

// Ключи ошибок валидации: имена полей контракта -> поля формы
const CONTRACT_FIELD_MAP = {
  bookingWindowDays: 'bookingWindowDays',
  workStart: 'workStart',
  workEnd: 'workEnd',
  lunchBreakStart: 'lunchBreakStart',
  lunchBreakEnd: 'lunchBreakEnd'
} as const

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/

// Сетка слотов строится с шагом 30 минут от начала рабочего дня: начало должно быть выровнено
const GRID_STEP_MINUTES = 30
const isOnGrid = (time: string) => Number(time.slice(3, 5)) % GRID_STEP_MINUTES === 0

// Сервер отдаёт plainTime (HH:mm:ss), а TimeInput без секунд работает с HH:mm
const toInputValue = (value: string | null | undefined) => (value ? value.slice(0, 5) : '')
// Обратно в контракт: plainTime с секундами
const toContractValue = (value: string) => (value ? `${value}:00` : null)

export default function SettingsPage() {
  const { data, isLoading } = useSettingsGet()
  const settings = data?.data
  const queryClient = useQueryClient()

  // null до первого редактирования: показываем значение с сервера без синхронизации через эффект
  const [editedDays, setEditedDays] = useState<string | number | null>(null)
  const [editedWorkStart, setEditedWorkStart] = useState<string | null>(null)
  const [editedWorkEnd, setEditedWorkEnd] = useState<string | null>(null)
  const [editedLunchStart, setEditedLunchStart] = useState<string | null>(null)
  const [editedLunchEnd, setEditedLunchEnd] = useState<string | null>(null)
  const days = editedDays ?? settings?.bookingWindowDays ?? ''
  const workStart = editedWorkStart ?? toInputValue(settings?.workStart)
  const workEnd = editedWorkEnd ?? toInputValue(settings?.workEnd)
  const lunchStart = editedLunchStart ?? toInputValue(settings?.lunchBreakStart)
  const lunchEnd = editedLunchEnd ?? toInputValue(settings?.lunchBreakEnd)
  const [fieldErrors, setFieldErrors] = useState<SettingsFieldErrors>({})

  const updateMutation = useMutation({
    mutationFn: (request: UpdateCalendarSettingsRequest) => settingsUpdate(request),
    onSuccess: async response => {
      if (response.status === 200) {
        const window = `Окно записи: ${response.data.bookingWindowDays} дней`
        const workDay = `рабочий день: ${response.data.workStart.slice(0, 5)}–${response.data.workEnd.slice(0, 5)}`
        const lunch = response.data.lunchBreakStart
          ? `, перерыв: ${response.data.lunchBreakStart.slice(0, 5)}–${response.data.lunchBreakEnd?.slice(0, 5) ?? ''}`
          : ''
        notifications.show({
          title: 'Настройки сохранены',
          message: `${window}, ${workDay}${lunch}`,
          color: 'green'
        })
        setFieldErrors({})
        await queryClient.invalidateQueries({ queryKey: getSettingsGetQueryKey() })
        return
      }

      const errors = problemFieldErrors(response.data, CONTRACT_FIELD_MAP)
      setFieldErrors(errors)
      if (Object.keys(errors).length === 0) {
        notifications.show({
          title: response.data.title ?? 'Некорректный запрос',
          message: response.data.detail ?? 'Проверьте значение окна записи',
          color: 'red'
        })
      }
    }
  })

  const submit = () => {
    const errors: SettingsFieldErrors = {}

    const windowDays = Number(days)
    if (!Number.isInteger(windowDays) || windowDays < 1) {
      errors.bookingWindowDays = 'Окно записи: целое число дней, минимум 1'
    }

    const hasWorkStart = workStart.trim() !== ''
    const hasWorkEnd = workEnd.trim() !== ''
    if (!hasWorkStart) {
      errors.workStart = 'Укажите начало рабочего дня'
    }
    if (!hasWorkEnd) {
      errors.workEnd = 'Укажите конец рабочего дня'
    }

    let workDayValid = hasWorkStart && hasWorkEnd
    if (workDayValid) {
      if (!TIME_PATTERN.test(workStart) || !TIME_PATTERN.test(workEnd)) {
        const message = 'Время в формате ЧЧ:ММ'
        errors.workStart = message
        errors.workEnd = message
        workDayValid = false
      } else if (workStart >= workEnd) {
        errors.workEnd = 'Конец рабочего дня должен быть позже начала'
        workDayValid = false
      } else if (!isOnGrid(workStart)) {
        errors.workStart = 'Начало рабочего дня должно быть кратно 30 минутам (например, 09:00 или 09:30)'
        workDayValid = false
      }
    }

    const hasLunchStart = lunchStart.trim() !== ''
    const hasLunchEnd = lunchEnd.trim() !== ''
    if (hasLunchStart && !hasLunchEnd) {
      errors.lunchBreakEnd = 'Укажите конец перерыва или очистите начало'
    } else if (!hasLunchStart && hasLunchEnd) {
      errors.lunchBreakStart = 'Укажите начало перерыва или очистите конец'
    } else if (hasLunchStart && hasLunchEnd) {
      if (!TIME_PATTERN.test(lunchStart) || !TIME_PATTERN.test(lunchEnd)) {
        const message = 'Время в формате ЧЧ:ММ'
        errors.lunchBreakStart = message
        errors.lunchBreakEnd = message
      } else if (lunchStart >= lunchEnd) {
        errors.lunchBreakEnd = 'Конец перерыва должен быть позже начала'
      } else if (workDayValid && (lunchStart < workStart || lunchEnd > workEnd)) {
        const message = `Перерыв должен помещаться в рабочий день ${workStart}–${workEnd}`
        errors.lunchBreakStart = message
        errors.lunchBreakEnd = message
      }
    }

    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    updateMutation.mutate({
      bookingWindowDays: windowDays,
      workStart: toContractValue(workStart) ?? '',
      workEnd: toContractValue(workEnd) ?? '',
      lunchBreakStart: hasLunchStart ? toContractValue(lunchStart) : null,
      lunchBreakEnd: hasLunchEnd ? toContractValue(lunchEnd) : null
    })
  }

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        <Title order={1}>Настройки</Title>

        {isLoading ? (
          <Skeleton height={200} radius="sm" />
        ) : (
          <Stack gap="md" maw={480}>
            <Text c="dimmed">
              Окно записи: сколько календарных дней вперёд гость может выбрать слот, включая текущий день.
            </Text>
            <NumberInput
              label="Окно записи (дней)"
              withAsterisk
              min={1}
              value={days}
              error={fieldErrors.bookingWindowDays}
              onChange={value => {
                setEditedDays(value)
                setFieldErrors(prev => ({ ...prev, bookingWindowDays: undefined }))
              }}
            />
            <Text c="dimmed">
              Рабочий день: границы, в которых строится сетка слотов и помещаются записи.
            </Text>
            <TimeInput
              label="Начало рабочего дня"
              value={workStart}
              error={fieldErrors.workStart}
              onChange={event => {
                setEditedWorkStart(event.currentTarget.value)
                setFieldErrors(prev => ({ ...prev, workStart: undefined }))
              }}
            />
            <TimeInput
              label="Конец рабочего дня"
              value={workEnd}
              error={fieldErrors.workEnd}
              onChange={event => {
                setEditedWorkEnd(event.currentTarget.value)
                setFieldErrors(prev => ({ ...prev, workEnd: undefined }))
              }}
            />
            <Text c="dimmed">
              Перерыв: промежуток внутри рабочего дня, в который гости не могут записаться.
              Оставьте поля пустыми, чтобы отключить перерыв.
            </Text>
            <TimeInput
              label="Начало перерыва"
              value={lunchStart}
              error={fieldErrors.lunchBreakStart}
              onChange={event => {
                setEditedLunchStart(event.currentTarget.value)
                setFieldErrors(prev => ({ ...prev, lunchBreakStart: undefined }))
              }}
            />
            <TimeInput
              label="Конец перерыва"
              value={lunchEnd}
              error={fieldErrors.lunchBreakEnd}
              onChange={event => {
                setEditedLunchEnd(event.currentTarget.value)
                setFieldErrors(prev => ({ ...prev, lunchBreakEnd: undefined }))
              }}
            />
            <Button loading={updateMutation.isPending} onClick={submit} w="fit-content">
              Сохранить
            </Button>
          </Stack>
        )}
      </Stack>
    </Container>
  )
}
