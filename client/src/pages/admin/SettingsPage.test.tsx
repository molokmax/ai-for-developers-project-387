import { fireEvent, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/helpers'
import SettingsPage from './SettingsPage'

describe('SettingsPage (настройки календаря)', () => {
  it('показывает текущее окно записи', async () => {
    mockFetch([{ url: '/api/settings', status: 200, body: { bookingWindowDays: 14 } }])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const input = await screen.findByLabelText(/Окно записи/)
    expect((input as HTMLInputElement).value).toBe('14')
  })

  it('сохраняет новое значение окна записи', async () => {
    const fetchMock = mockFetch([
      { url: '/api/settings', status: 200, body: { bookingWindowDays: 14 } },
      { url: '/api/settings', method: 'PUT', status: 200, body: { bookingWindowDays: 30 } },
    ])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const input = await screen.findByLabelText(/Окно записи/)
    fireEvent.change(input, { target: { value: '30' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    await waitFor(() => {
      const updateCall = fetchMock.mock.calls.find(
        ([url, init]) => String(url) === '/api/settings' && init?.method === 'PUT',
      )
      expect(updateCall).toBeDefined()
      expect(JSON.parse(String(updateCall![1]!.body))).toEqual({ bookingWindowDays: 30 })
    })

    expect(await screen.findByText('Настройки сохранены')).toBeDefined()
  })

  it('не отправляет невалидное значение', async () => {
    const fetchMock = mockFetch([{ url: '/api/settings', status: 200, body: { bookingWindowDays: 14 } }])

    renderPage(<SettingsPage />, { route: '/admin/settings', path: '/admin/settings' })

    const input = await screen.findByLabelText(/Окно записи/)
    fireEvent.change(input, { target: { value: '0' } })
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }))

    expect(await screen.findByText('Окно записи: целое число дней, минимум 1')).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0)
  })
})
