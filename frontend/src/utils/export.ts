export type Column = {
  title?: React.ReactNode;
  dataIndex?: string | number;
  key?: string | number;
  render?: (value: any, record: any, index: number) => React.ReactNode;
  [key: string]: any;
};

export function exportTableToCsv(filename: string, columns: Column[], dataSource: readonly any[]) {
  const header = columns
    .filter((col) => col.dataIndex)
    .map((col) => {
      const title = typeof col.title === 'string' ? col.title : String(col.title ?? col.dataIndex);
      return `"${title}"`;
    })
    .join(',');

  const rows = dataSource.map((row) =>
    columns
      .filter((col) => col.dataIndex != null)
      .map((col) => {
        const key = String(col.dataIndex);
        const value = row[key];
        const text = col.render ? col.render(value, row, 0) : value;
        const stringValue = text == null ? '' : String(text);
        const escaped = stringValue.replace(/"/g, '""');
        return `"${escaped}"`;
      })
      .join(',')
  );

  const csv = [header, ...rows].join('\n');
  const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${filename}.csv`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
