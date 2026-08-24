export type ChatSource = {
  fileName: string
  pageNumber: number
  documentId: string
  downloadUrl: string
}

export type ChatResponse = {
  answer: string
  sources: ChatSource[]
}
