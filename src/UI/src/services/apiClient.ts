import axios from 'axios'

type ApiErrorResponse = {
  error?: string
  detail?: string
  title?: string
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL

if (!apiBaseUrl) {
  throw new Error('VITE_API_BASE_URL must contain the absolute ASP.NET Core API URL.')
}

/** Shared Axios client for the separately hosted ASP.NET Core API. */
export const apiClient = axios.create({
  baseURL: apiBaseUrl,
})

/** Reads the validation or problem-details message returned by the API. */
export function getApiErrorMessage(error: unknown, fallbackMessage: string): string {
  if (!axios.isAxiosError<ApiErrorResponse>(error)) return fallbackMessage

  return error.response?.data.error
    ?? error.response?.data.detail
    ?? error.response?.data.title
    ?? fallbackMessage
}

/** Converts a relative API download path into a URL on the API host. */
export function createApiUrl(path: string): string {
  return new URL(path, new URL(apiBaseUrl).origin).toString()
}
