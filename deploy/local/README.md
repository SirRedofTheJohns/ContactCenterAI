# Entorno local y evidencia por fase

Estado: **B01 CLOSED**. Pasaron 12 checks offline, siete checks SQL/OIDC y dos logins reales. Diagnóstico detenido; retiro de confianza temporal en el perfil Windows original no confirmado tras cerrar el terminal. Ver [evidencia B01](../../docs/progress/b01-runtime-identity.md).

## Perfil fijado

SDK .NET **10.0.401**, runtime **10.0.12**; SQL Server **2022 Developer** y Keycloak **26.7.5** por digest en [image-lock.json](image-lock.json). Developer se limita a desarrollo/pruebas. Keycloak usa `start-dev`/H2 local, ocho cuentas sintéticas y HTTP loopback. Ambos contenedores requieren Docker Linux x86-64; su presupuesto inicial de memoria suma 4 GiB, además de Docker/WSL/host. Capacidad real pendiente de medir.

Cliente confidencial `contactcenterai-bff`: Code+PKCE S256, callbacks exactos `https://localhost:7443/signin-oidc` (diagnóstico) y `https://localhost:7451/signin-oidc` (producto candidato); implicit/password/service-account deshabilitados. SQL publica `127.0.0.1:14333`; Keycloak `127.0.0.1:8080`. La actualización de un realm persistido se hace con `eng/Configure-ProductIdentity.py`; no depende de reimportar el JSON.

Fuentes: [SDK .NET](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [SQL containers](https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver16), [ediciones SQL](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver16), [Keycloak Docker](https://www.keycloak.org/getting-started/getting-started-docker), [realm import](https://www.keycloak.org/server/importExport).

## Ejecución desde la raíz del repositorio

Atajo Windows (funciona desde Windows PowerShell 5.1 o PowerShell 7): después de revisar los términos SQL Developer enlazados abajo, `./eng/Continue-B01.cmd -AcceptSqlDeveloperLicense`. El launcher usa PowerShell 7 incluido en este equipo, selecciona el SDK/feed ya verificados si existen, conserva secretos locales, inicia los contenedores y espera SQL health/discovery antes de ejecutar el spike live. El parámetro registra aceptación de esos términos en `.env`; no se usa en la ejecución del agente. `./eng/Continue-B01.cmd -OfflineOnly` solo ejecuta checks offline. En otro checkout se requieren PowerShell 7 y el SDK fijado instalados.

En Windows el script apunta explícitamente a `dockerDesktopLinuxEngine`, coherente con el contexto `desktop-linux` comprobado por el usuario. Se puede seleccionar otro endpoint con `-DockerEndpoint` sin cambiar la configuración global de Docker. Opciones de CLI: [Docker](https://docs.docker.com/reference/cli/docker/); espera de readiness: [Compose up](https://docs.docker.com/reference/cli/docker/compose/up/).

PowerShell 7.2+ y el SDK fijado. En este workspace ya se descargó/verificó el SDK; desde el repositorio:

```powershell
$taskSdk = (Resolve-Path '../../work/runtimes/dotnet/dotnet.exe').Path
./eng/Initialize-LocalEnvironment.ps1
```

En otro checkout con el SDK instalado, usar `$taskSdk = 'dotnet'`. El SDK del workspace no se incluye en el ZIP.

El inicializador genera secretos aleatorios en `deploy/local/.env`, excluido de Git/ZIP, y conserva un archivo existente. Revisar [términos SQL Developer](https://go.microsoft.com/fwlink/?LinkId=746388) y registrar `CCAI_ACCEPT_SQL_EULA=Y` localmente antes de arrancar SQL. No compartir ese archivo ni capturas de su contenido.

```powershell
./eng/Start-LocalDependencies.ps1
./eng/Invoke-B01.ps1 -DotNetExe $taskSdk
```

Start exige motor Linux accesible y valida Compose sin imprimir secretos. Invoke restaura con lockfile, compila y prueba configuración, SQL, discovery y JWKS. Un servicio ausente falla el gate. Las variables de su proceso se restauran al terminar. Para checks offline:

```powershell
./eng/Invoke-B01.ps1 -DotNetExe $taskSdk -OfflineOnly
```

Si .NET falla al negociar TLS con NuGet, reparar el entorno sin desactivar validación TLS/firma. Esta sesión usó un feed temporal `work/nuget-feed`, descargado por HTTPS verificado y SHA-512 contrastado con el catálogo oficial; se puede seleccionar aquí con `-PackageSource (Resolve-Path '../../work/nuget-feed').Path`. NuGet conserva locked-mode y verificación de firma normal. El feed no está en el ZIP. Inventario/advisories: [b01-dependencies.json](../../docs/progress/b01-dependencies.json).

## Login interactivo

### Diagnóstico de conexión SQL

Con los contenedores ya iniciados, ejecutar `./eng/Continue-B01.cmd -CheckOnly -CompareSqlNetworking`. Evita arrancar/descargar contenedores y conserva datos/secretos. Primero prueba el networking nativo seleccionado; si falla, compara managed en otro proceso, **con Encrypt Mandatory en ambos**. Se imprimen fase, Number/State/Class/HResult y códigos de causa conocidos; no el texto arbitrario de excepción. Managed es exclusivamente diagnóstico según [Microsoft](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches?view=sql-server-ver17#enable-managed-networking-on-windows), y no cierra el gate de la baseline si el cliente nativo sigue fallando. El endpoint SQL es `tcp:127.0.0.1,14333`, coincidente con el bind IPv4 de Compose.

### Diagnóstico interactivo OIDC

Con los contenedores actuales en ejecución, desde Windows PowerShell 5.1 o PowerShell 7:

```powershell
./eng/Start-B01Login.cmd -TrustLocalCertificate
```

El flag autoriza **confianza temporal de un certificado localhost nuevo en CurrentUser Root**. El asistente confirma su thumbprint contra el proceso que acaba de iniciar. Mantiene la validación HTTPS activa, verifica 401 para `/proof` sin sesión y abre dos ventanas de Edge con perfiles locales separados. No cambia certificados del equipo/otros usuarios. Al terminar normalmente solicita parada al servidor y espera la liberación de su clave; después retira el certificado de confianza exacto. Una parada forzada se informa por separado.

Para cada cuenta, pega la contraseña sintética con Ctrl+V y pulsa Sign In. El asistente la copia al portapapeles sin imprimirla y la limpia al terminar si sigue allí. Sobrescribe el texto anterior del portapapeles. Si el realm persistido pide completar perfil, usa estos datos ficticios; [VerifyProfile está habilitado por defecto en Keycloak](https://www.keycloak.org/docs/latest/server_admin/index.html#_user-profile):

| Usuario | Nombre | Apellido | Email ficticio | Subject esperado |
|---|---|---|---|---|
| customer-a | Customer | A | customer-a@example.invalid | 10000000-0000-0000-0000-000000000001 |
| customer-b | Customer | B | customer-b@example.invalid | 10000000-0000-0000-0000-000000000002 |

Se espera rol Customer en ambas cuentas. Solo después de dos PASS guarda `docs/progress/b01-login-evidence.json`, sin secretos. Un fallo/timeout no cierra B01. La evidencia incluye flujo Code+PKCE S256, callback exacto, token validado por el middleware, subject/rol esperado, atributos reales de cookie y su regreso HTTPS. No acredita autorización de miembros/reservas, CSRF o revocación. Esos controles quedan para B03.

El protocolo usa el [handler OIDC oficial ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0), cliente confidencial y validación RS256/issuer/audience/lifetime. El asistente no recibe la contraseña en una página propia: la introduce el usuario en Keycloak. El portapapeles se limita al fixture local de este spike. El realm no emite MemberRef/tenantId como autoridad; [member-bindings.json](member-bindings.json) es seed servidor para SQL futuro.

`./eng/Start-B01Login.cmd -BuildOnly` compila/ejecuta offline sin navegador ni cambios de confianza. El primer intento falló en HTTPS; la carga de clave se corrigió y se verificaron el preflight y ambos logins reales en el navegador integrado de Codex. Los códigos TLS_CLIENT/TLS_SERVER/TLS_CERTIFICATE ayudan a distinguir el siguiente resultado sin exportar mensajes de proveedor. Si Edge no está instalado, usar el diagnóstico manual:

```powershell
./eng/Start-OidcDiagnostic.ps1 -DotNetExe $taskSdk
```

En otra terminal, confiar en la parte pública de esta ejecución, abrir `/login?account=customer-a` y después `/login?account=customer-b` en sesiones separadas; revisar `/proof` y los checks de `.local/b01-login/results.json`:

```powershell
$taskCertificate = Import-Certificate -FilePath './.local/diagnostic-certs/localhost.cer' -CertStoreLocation 'Cert:\CurrentUser\Root'
# Retirar únicamente ese certificado al terminar:
Remove-Item -LiteralPath ('Cert:\CurrentUser\Root\' + $taskCertificate.Thumbprint)
```

Si el asistente fue terminado forzosamente sin ejecutar su limpieza, la parte pública exacta queda en `.local/diagnostic-certs/localhost.cer`; comprobar su thumbprint y retirar solamente ese certificado de CurrentUser Root. No limpiar todo el almacén. Cerrar también las ventanas de prueba y el servidor de esta ejecución.

El certificado tiene vigencia de siete días. En Windows, su clave se carga en un contenedor temporal del usuario para ser compatible con Schannel; el PKCS12 solo existe en memoria y se omite PersistKeySet. La parada normal libera el certificado y su contenedor. En otros sistemas usa EphemeralKeySet. Esto corrige el [fallo conocido de claves efímeras en Windows](https://learn.microsoft.com/en-us/dotnet/core/extensions/sslstream-troubleshooting#handshake-failed-with-ephemeral-keys). Las claves Data Protection de este spike están en `.local/diagnostic-keys` sin cifrado de archivo; perfiles temporales en `.local/b01-browser`, evidencia temporal en `.local/b01-login`. Todo `.local` está excluido de Git/ZIP y no es configuración productiva de B03. No se desactiva TLS ni se usa el certificado fuera de localhost.

## Persistencia y parada

```powershell
docker --config ./.local/docker-client --host npipe:////./pipe/dockerDesktopLinuxEngine compose --env-file ./deploy/local/.env -f ./deploy/local/compose.yaml down
```

Se conservan los volúmenes. Keycloak omite importar un realm existente; editar el JSON no actualiza ese realm. Cambiar `.env` tampoco rota contraseñas persistidas. Planificar actualización explícita y probar los controles afectados; no eliminar volúmenes por defecto. B04 ya creó `ContactCenterAI_Operations` y login `ccai_app` de permisos por tabla. `sa` se usa solo en diagnósticos/provisioning offline, nunca en el runtime producto. B05 creará otra base para la fuente simulada.

## Candidato B03/B04

Ver [estado del slice, reproducción y gates](../../docs/progress/b03-b04-ingress.md). `eng/Invoke-IngressChecks.ps1` no necesita Docker ni permisos de certificado: usa el pipeline ASP.NET real con puertos fixture aislados. Eso no reemplaza el roundtrip de producto pendiente. `eng/Provision-Operational.py` aplica DDL versionado aditivo y semillas ficticias con TLS completo; `eng/Check-OperationalSchema.py` verifica constraints en transacciones siempre revertidas. Son herramientas de datos offline, no un backend Python.

No hay UI runnable ni worker consumiendo inbox todavía. API escucha health 7450 HTTP; producto exige HTTPS 7451 y credenciales/clave suministradas al proceso. No cargar `.env` en logs/capturas ni desactivar TLS para sortear el perfil Windows restringido.
