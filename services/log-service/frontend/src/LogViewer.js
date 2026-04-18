import React, { useState, useEffect } from 'react';
import { fetchLogs } from './api';

function LogViewer() {
  const [logs, setLogs] = useState([]);
  const [filter, setFilter] = useState({ service: '', level: '', correlationId: '' });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadLogs();
  }, [filter]);

  async function loadLogs() {
    setLoading(true);
    try {
      const data = await fetchLogs(filter);
      setLogs(data.logs);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h2>Logs Viewer</h2>
      <div>
        <input placeholder="Service" value={filter.service} onChange={e => setFilter({...filter, service: e.target.value})} />
        <input placeholder="Level" value={filter.level} onChange={e => setFilter({...filter, level: e.target.value})} />
        <input placeholder="Correlation ID" value={filter.correlationId} onChange={e => setFilter({...filter, correlationId: e.target.value})} />
        <button onClick={loadLogs}>Filter</button>
      </div>
      <table border="1">
        <thead><tr><th>Timestamp</th><th>Service</th><th>Level</th><th>Message</th><th>CorrelationId</th></tr></thead>
        <tbody>
        {logs.map(log => (
          <tr key={log.id}>
            <td>{new Date(log.timestamp).toLocaleString()}</td>
            <td>{log.service}</td>
            <td>{log.level}</td>
            <td>{log.message}</td>
            <td>{log.correlationId}</td>
          </tr>
        ))}
        </tbody>
      </table>
      {loading && <p>Loading...</p>}
    </div>
  );
}

export default LogViewer;
