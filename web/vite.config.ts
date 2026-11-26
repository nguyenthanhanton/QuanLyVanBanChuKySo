import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': {
      target: 'http://127.0.0.1:5000', changeOrigin: true,
      configure(proxy) {
        proxy.on('error', (_error, _request, response) => {
          if ('writeHead' in response && !response.headersSent) {
            response.writeHead(502, {'Content-Type':'application/json; charset=utf-8'});
            response.end(JSON.stringify({message:'Không thể kết nối máy chủ API. Hãy kiểm tra backend và kết nối mạng.'}));
          }
        });
      },
    } },
  },
});
