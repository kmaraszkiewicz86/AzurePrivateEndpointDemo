import type { ChatResponse } from '../models/chat'
import { apiClient, createApiUrl, getApiErrorMessage } from './apiClient'

/** Sends a question to the API and returns its grounded Azure OpenAI response. */
export async function askChatbot(question: string): Promise<ChatResponse> {
  try {
    const response = await apiClient.post<ChatResponse>('/chatbot', { question })
    return {
      ...response.data,
      sources: response.data.sources.map((source) => ({
        ...source,
        downloadUrl: createApiUrl(source.downloadUrl),
      })),
    }
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'Could not ask the chatbot.'))
  }
}
