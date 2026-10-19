import { fireEvent, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/helpers'
import SettingsPage from './SettingsPage'

const DEFAULT_WORK_DAY = { workStart: '09:00:00', workEnd: '18:00:00' }

describe('SettingsPage (настройки календаря)', () => {
  it('показывает текущие окно записи, рабочий день и перерыв', async () => {
    mockFetch([
      {
        url: '/api/settings',
        status: 200,
        body: {
          bookingWindowDays: 14,
          ...DEFAULT_WORK_DAY,
          lunchBreakStart: '13:00:00',
          lunchBreakEnd: '14:00:00'
        }
      }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const windowInput = await screen.findByLabelText(/Окно записи/)
    expect((windowInput as HTMLInputElement).value).toBe('14')

    expect((screen.getByLabelText('Начало рабочего дня') as HTMLInputElement).value).toBe('09:00')
    expect((screen.getByLabelText('Конец рабочего дня') as HTMLInputElement).value).toBe('18:00')

    expect((screen.getByLabelText('Начало перерыва') as HTMLInputElement).value).toBe('13:00')
    expect((screen.getByLabelText('Конец перерыва') as HTMLInputElement).value).toBe('14:00')
  })

  it('сохраняет новое значение окна записи без перерыва', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } },
      {
        url: '/api/settings',
        method: 'PUT',
        status: 200,
        body: { bookingWindowDays: 30, ...DEFAULT_WORK_DAY }
      }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const input = await screen.findByLabelText(/Окно записи/)
    fireEvent.change(input, { target: { value: '30' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    await waitFor(() => {
      const updateCall = fetchMock.mock.calls.find(
        ([url, init]) => String(url) === '/api/settings' && init?.method === 'PUT'
      )
      expect(updateCall).toBeDefined()
      expect(JSON.parse(String(updateCall![1]!.body))).toEqual({
        bookingWindowDays: 30,
        workStart: '09:00:00',
        workEnd: '18:00:00',
        lunchBreakStart: null,
        lunchBreakEnd: null
      })
    })

    expect(await screen.findByText('Настройки сохранены')).toBeDefined()
  })

  it('сохраняет рабочий день и перерыв в формате контракта', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } },
      {
        url: '/api/settings',
        method: 'PUT',
        status: 200,
        body: {
          bookingWindowDays: 14,
          workStart: '08:00:00',
          workEnd: '20:00:00',
          lunchBreakStart: '13:00:00',
          lunchBreakEnd: '14:00:00'
        }
      }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    fireEvent.change(await screen.findByLabelText('Начало рабочего дня'), { target: { value: '08:00' } })
    fireEvent.change(screen.getByLabelText('Конец рабочего дня'), { target: { value: '20:00' } })
    fireEvent.change(screen.getByLabelText('Начало перерыва'), { target: { value: '13:00' } })
    fireEvent.change(screen.getByLabelText('Конец перерыва'), { target: { value: '14:00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    await waitFor(() => {
      const updateCall = fetchMock.mock.calls.find(
        ([url, init]) => String(url) === '/api/settings' && init?.method === 'PUT'
      )
      expect(updateCall).toBeDefined()
      expect(JSON.parse(String(updateCall![1]!.body))).toEqual({
        bookingWindowDays: 14,
        workStart: '08:00:00',
        workEnd: '20:00:00',
        lunchBreakStart: '13:00:00',
        lunchBreakEnd: '14:00:00'
      })
    })
  })

  it('не отправляет невалидное значение окна записи', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const input = await screen.findByLabelText(/Окно записи/)
    fireEvent.change(input, { target: { value: '0' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect(await screen.findByText('Окно записи: целое число дней, минимум 1')).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })

  it('не отправляет рабочий день, если конец раньше начала', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    fireEvent.change(await screen.findByLabelText('Начало рабочего дня'), { target: { value: '18:00' } })
    fireEvent.change(screen.getByLabelText('Конец рабочего дня'), { target: { value: '09:00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect(await screen.findByText('Конец рабочего дня должен быть позже начала')).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })

  it('не отправляет рабочий день с началом не кратно 30 минутам', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    fireEvent.change(await screen.findByLabelText('Начало рабочего дня'), { target: { value: '09:15' } })
    fireEvent.change(screen.getByLabelText('Конец рабочего дня'), { target: { value: '18:00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect(
      await screen.findByText('Начало рабочего дня должно быть кратно 30 минутам (например, 09:00 или 09:30)')
    ).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })

  it('не отправляет перерыв, выходящий за рабочий день', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    fireEvent.change(await screen.findByLabelText('Начало перерыва'), { target: { value: '08:00' } })
    fireEvent.change(screen.getByLabelText('Конец перерыва'), { target: { value: '09:00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect((await screen.findAllByText('Перерыв должен помещаться в рабочий день 09:00–18:00')).length).toBeGreaterThan(0)
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })

  it('не отправляет перерыв, если задана только одна граница', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14, ...DEFAULT_WORK_DAY } }
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    fireEvent.change(await screen.findByLabelText('Начало перерыва'), { target: { value: '13:00' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect(await screen.findByText('Укажите конец перерыва или очистите начало')).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })
})
