import type { UploadedDocument } from '../models/document'
import { apiClient, createApiUrl, getApiErrorMessage } from './apiClient'

/** Uploads one PDF to the separately hosted ASP.NET Core API. */
export async function uploadPdf(file: File): Promise<UploadedDocument> {
  const formData = new FormData()
  formData.append('file', file)

  try {
    const response = await apiClient.post<UploadedDocument>('/Documents', formData)
    return {
      ...response.data,
      downloadUrl: createApiUrl(response.data.downloadUrl),
    }
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'Could not upload the PDF.'))
  }
}

/** Deletes one PDF through the API; Event Grid handles search-index cleanup asynchronously. */
export async function deletePdf(blobName: string): Promise<void> {
  try {
    await apiClient.delete(`/Documents/${encodeURIComponent(blobName)}`)
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'Could not delete the PDF.'))
  }
}
