import type { IndexedDocument } from '../models/indexedDocument'
import { apiClient, createApiUrl, getApiErrorMessage } from './apiClient'

/** Retrieves indexed files and their extracted content through the API, without an AI model call. */
export async function getIndexedDocuments(signal?: AbortSignal): Promise<IndexedDocument[]> {
  try {
    const response = await apiClient.get<IndexedDocument[]>('/IndexedDocuments', { signal })
    return response.data.map((document) => ({
      ...document,
      downloadUrl: createApiUrl(document.downloadUrl),
    }))
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'Could not load indexed files.'))
  }
}
