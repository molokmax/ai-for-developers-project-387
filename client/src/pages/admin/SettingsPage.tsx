import { Button, Container, NumberInput, Skeleton, Stack, Text, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { getSettingsGetQueryKey, settingsUpdate, useSettingsGet } from '../../api/gen'
import { problemFieldErrors, type FieldErrors } from '../../api/problemErrors'
import type { UpdateCalendarSettingsRequest } from '../../api/gen/model'

type SettingsFieldErrors = FieldErrors<'bookingWindowDays'>

// Ключи ошибок валидации: имена полей контракта -> поля формы
const CONTRACT_FIELD_MAP = { bookingWindowDays: 'bookingWindowDays' } as const

export default function SettingsPage() {
  const { data, isLoading } = useSettingsGet()
  const settings = data?.data
  const queryClient = useQueryClient()

  // null до первого редактирования: показываем значение с сервера без синхронизации через эффект
  const [editedDays, setEditedDays] = useState<string | number | null>(null)
  const days = editedDays ?? settings?.bookingWindowDays ?? ''
  const [fieldErrors, setFieldErrors] = useState<SettingsFieldErrors>({})

  const updateMutation = useMutation({
    mutationFn: (request: UpdateCalendarSettingsRequest) => settingsUpdate(request),
    onSuccess: async response => {
      if (response.status === 200) {
        notifications.show({
          title: 'Настройки сохранены',
          message: `Окно записи: ${response.data.bookingWindowDays} дней`,
          color: 'green',
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
          color: 'red',
        })
      }
    },
  })

  const submit = () => {
    const errors: SettingsFieldErrors = {}
    const value = Number(days)
    if (!Number.isInteger(value) || value < 1) {
      errors.bookingWindowDays = 'Окно записи: целое число дней, минимум 1'
    }
    setFieldErrors(errors)
    if (errors.bookingWindowDays) return
    updateMutation.mutate({ bookingWindowDays: value })
  }

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        <Title order={1}>Настройки</Title>

        {isLoading ? (
          <Skeleton height={120} radius="sm" />
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
            <Button loading={updateMutation.isPending} onClick={submit} w="fit-content">
              Сохранить
            </Button>
          </Stack>
        )}
      </Stack>
    </Container>
  )
}
