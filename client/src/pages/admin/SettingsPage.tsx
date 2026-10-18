import { Button, Container, NumberInput, Skeleton, Stack, Text, Title } from '@mantine/core'
import { TimeInput } from '@mantine/dates'
import { notifications } from '@mantine/notifications'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { getSettingsGetQueryKey, settingsUpdate, useSettingsGet } from '../../api/gen'
import { problemFieldErrors, type FieldErrors } from '../../api/problemErrors'
import type { UpdateCalendarSettingsRequest } from '../../api/gen/model'

type SettingsFieldErrors = FieldErrors<'bookingWindowDays' | 'lunchBreakStart' | 'lunchBreakEnd'>

// Ключи ошибок валидации: имена полей контракта -> поля формы
const CONTRACT_FIELD_MAP = {
  bookingWindowDays: 'bookingWindowDays',
  lunchBreakStart: 'lunchBreakStart',
  lunchBreakEnd: 'lunchBreakEnd'
} as const

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/
const WORK_START = '09:00'
const WORK_END = '18:00'

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
  const [editedLunchStart, setEditedLunchStart] = useState<string | null>(null)
  const [editedLunchEnd, setEditedLunchEnd] = useState<string | null>(null)
  const days = editedDays ?? settings?.bookingWindowDays ?? ''
  const lunchStart = editedLunchStart ?? toInputValue(settings?.lunchBreakStart)
  const lunchEnd = editedLunchEnd ?? toInputValue(settings?.lunchBreakEnd)
  const [fieldErrors, setFieldErrors] = useState<SettingsFieldErrors>({})

  const updateMutation = useMutation({
    mutationFn: (request: UpdateCalendarSettingsRequest) => settingsUpdate(request),
    onSuccess: async response => {
      if (response.status === 200) {
        notifications.show({
          title: 'Настройки сохранены',
          message: response.data.lunchBreakStart
            ? `Окно записи: ${response.data.bookingWindowDays} дней, обед: ${response.data.lunchBreakStart.slice(0, 5)}–${response.data.lunchBreakEnd?.slice(0, 5) ?? ''}`
            : `Окно записи: ${response.data.bookingWindowDays} дней`,
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
      } else if (lunchStart < WORK_START || lunchEnd > WORK_END) {
        const message = `Перерыв должен помещаться в рабочий день ${WORK_START}–${WORK_END}`
        errors.lunchBreakStart = message
        errors.lunchBreakEnd = message
      }
    }

    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    updateMutation.mutate({
      bookingWindowDays: windowDays,
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
              Обеденный перерыв: промежуток внутри рабочего дня, в который гости не могут записаться.
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
