
Pruebalo en http://108.181.101.20:7779/debug! (Necesitas usar una clave de acceso para crear una cuenta)
Espero que este proyecto ayude a alguien que quiera hacer un sistema multijugador

# Kitty Kat Kats

**Juego multijugador basura** donde los jugadores cuidan gatos virtuales, compiten en minijuegos y suben en el ranking global.

## Descripción del proyecto
Hola, soy zeep, este proyecto fue un prototipo de implementar multijugador desde 0 a un juego de turbowarp (scratch), no tengo el conocimiento de que arquitectura utilcie, me base en el multijugador de terraria para esto, adjunto documentacion (quiza generada por ia)

### Stack técnico

- **Servidor**: Lógica implementada desde cero en C# (.NET 8)
  - WebSocket para comunicación en tiempo real
  - HTTP server para la web y archivos estáticos
  - Base de datos SQLite con usuarios, gatos, items e inventarios
  - Sistema de paquetes propio (OpCodes) para autenticación, chat, ranking y gameplay
  - 
- **Cliente**: Interfaz del juego desarrollada en **Turbowarp** (mod de Scratch)

### Sistema de paquetes

Los mensajes se envían en texto plano con el formato `OpCode|arg1|arg2|...` (delimitador `|`). Cada paquete tiene un **OpCode** numérico que indica la acción (LOGIN, CHATMSG, EAT, PLAYMINIGAME, etc.) seguido de argumentos en cadena. Opcionalmente se soporta formato binario: 2 bytes para OpCode, 2 para cantidad de argumentos, y cada argumento como 4 bytes de longitud + contenido UTF-8. El servidor valida el OpCode y procesa los argumentos en cada handler.

### Arquitectura

La lógica del servidor está basada en la arquitectura de servidores de **Terraria** (TShock): game loop en segundo plano, manejo de clientes por WebSocket, base de datos SQLite y sistema de paquetes con OpCodes.

### Características

- Autenticación con códigos de invitación
- Chat en vivo entre jugadores
- Ranking top 10 global
- 6 gatos coleccionables con skins y equipables
- 3 minijuegos: Gatordia, Flappy Pollo, Moscon Dash
- Tienda, inventario y sistema de items consumibles/equipables

---

## Cómo iniciar el servidor 

### Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Dependencias del proyecto: Fleck (NuGet) y referencias a BCrypt.Net, Mono.Data.Sqlite, HttpServer (compatibles con TShock / Terraria server)

### Pasos

1. Clonar el repositorio y restaurar paquetes:
   ```bash
   dotnet restore
   ```

2. Crear `config.txt` en la raíz del proyecto (o junto al ejecutable) con la configuración deseada:
   ```
   WebSocketPort=3000
   HttpPort=8080
   WebRoot=
   ServerIP=
   ```

3. Ejecutar el servidor:
   ```bash
   dotnet run
   ```
   O publicar y ejecutar:
   ```bash
   dotnet publish -c Release -r linux-x64
   ./bin/Release/net8.0/linux-x64/publish/KittyServer
   ```

4. El servidor escribe `ip.txt` con la URL WebSocket (ej. `ws://192.168.1.x:3000`) para que los clientes se conecten. Abre la página web (servida en `HttpPort`) y el iframe cargará el juego de Turbowarp.

**Comandos de consola:** `/clients` (ver conectados), `/code N` (generar códigos de invitación), `/msg OPCODE|args` (enviar paquete de prueba), `exit` (cerrar servidor).

---

