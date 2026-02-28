/**
 * Extensión TurboWarp: Carga la IP de websocket desde ip.txt
 * 
 * Uso en tu proyecto:
 * 1. Crea una variable llamada "#websocket ip" (o como prefieras)
 * 2. Al inicio (ej: cuando se presione bandera verde), usa:
 *    set [#websocket ip] to [Obtener IP del archivo]
 * 
 * Requisitos:
 * - El archivo ip.txt debe estar en la carpeta raíz del proyecto
 * - Si usas un servidor local, ip.txt debe estar en la misma carpeta que index.html
 * - Para desarrollo: usa un servidor HTTP (ej: python -m http.server 8080)
 */

class WebSocketIPExtension {
  getInfo() {
    return {
      id: 'websocketip',
      name: 'WebSocket IP',
      color1: '#4C97FF',
      color2: '#3373CC',
      color3: '#2E5D8A',
      blocks: [
        {
          opcode: 'getIPFromFile',
          blockType: Scratch.BlockType.REPORTER,
          text: 'Obtener IP del archivo'
        }
      ]
    };
  }

  getIPFromFile() {
    // Intenta cargar ip.txt desde la raíz (misma carpeta que el HTML)
    return fetch('ip.txt')
      .then(response => {
        if (!response.ok) {
          throw new Error('No se encontró ip.txt');
        }
        return response.text();
      })
      .then(text => {
        return text.trim();
      })
      .catch(error => {
        console.error('Error al cargar ip.txt:', error);
        return 'localhost'; // Valor por defecto si falla
      });
  }
}

Scratch.extensions.register(new WebSocketIPExtension());
