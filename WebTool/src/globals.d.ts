declare module '*.json' {
  const value: any
  export default value
}

declare module '../api_item_names.json' {
  const value: Record<string, { Name: string; ShortName: string }>
  export default value
}
