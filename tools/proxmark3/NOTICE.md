# proxmark3.exe — aviso de terceiros

Este ficheiro (`proxmark3.exe`) é o cliente oficial do projeto **Proxmark3 Iceman fork**
(RfidResearchGroup/proxmark3), usado pelo FloppyGames para comunicar com um leitor/programador
Proxmark3 ligado por USB — uma alternativa a um leitor PC/SC genérico para gravar cartões
NFC/RFID Mifare Classic.

- **Origem**: <https://github.com/RfidResearchGroup/proxmark3>
- **Licença**: GPL-2.0 (ver o repositório de origem para o texto completo da licença)
- **Versão compilada**: tag `v4.21611` ("BREAKMEIFYOUCAN!"), escolhida por corresponder à versão
  de `CAPABILITIES_VERSION` (7) usada pelo firmware de dispositivos Proxmark3 já em uso — versões
  mais recentes do cliente recusam-se a comunicar com firmware mais antigo.
- **Compilado a partir do código-fonte** com MSYS2 UCRT64 (`make client`), sem alterações ao
  código-fonte original.

O FloppyGames **nunca liga (link) este executável ao seu próprio código** — é invocado sempre
como um processo externo separado (`Process.Start`), tal como uma app chamaria `git.exe` ou
`ffmpeg.exe`. O FloppyGames em si mantém-se licenciado MIT (ver [LICENSE.md](../../LICENSE.md) na
raiz do repositório); este ficheiro isolado é que está sob os termos GPL-2.0 do projeto Proxmark3.

## DLLs de runtime (MSYS2 UCRT64)

Compilado com MSYS2, o `proxmark3.exe` precisa destes DLLs na mesma pasta — sem eles termina de
imediato com `0xC0000135` ("DLL not found") em qualquer PC que não tenha o MSYS2 no `PATH`
(verificado: com estes 9 DLLs ao lado e um `PATH` só com `C:\Windows`, arranca normalmente). São
copiados sem alterações de `ucrt64/bin` da instalação MSYS2 usada para compilar o cliente, e
invocados, tal como o executável, só dentro desse processo externo separado.

| DLL | Pacote MSYS2 (versão) | Licença |
|---|---|---|
| `libbz2-1.dll` | `mingw-w64-ucrt-x86_64-bzip2` 1.0.8-3 | bzip2 (tipo BSD) |
| `libgcc_s_seh-1.dll` | `mingw-w64-ucrt-x86_64-gcc-libs` 16.1.0-5 | GPL-3.0 com GCC Runtime Library Exception |
| `libjansson-4.dll` | `mingw-w64-ucrt-x86_64-jansson` 2.15.1-1 | MIT |
| `liblz4.dll` | `mingw-w64-ucrt-x86_64-lz4` 1.10.0-1 | BSD-2-Clause |
| `libpython3.14.dll` | `mingw-w64-ucrt-x86_64-python` 3.14.7-1 | PSF License |
| `libreadline8.dll` | `mingw-w64-ucrt-x86_64-readline` 8.3.003-1 | GPL-3.0 |
| `libtermcap-0.dll` | `mingw-w64-ucrt-x86_64-termcap` 1.3.1-7 | GPL-2.0 |
| `libwinpthread-1.dll` | `mingw-w64-ucrt-x86_64-libwinpthread` 14.0.0.r92.g818fa6510-1 | MIT / BSD |
| `zlib1.dll` | `mingw-w64-ucrt-x86_64-zlib` 1.3.2-2 | zlib |

O código-fonte de cada um está disponível nos pacotes MSYS2 correspondentes
(<https://packages.msys2.org>). Ao recompilar o cliente, estes DLLs têm de ser atualizados a
partir da mesma instalação MSYS2 — a lista exata pode mudar entre versões.

Para recompilar esta ferramenta a partir do código-fonte (por exemplo, para uma versão mais
recente compatível com o teu firmware):

```sh
git clone --depth 1 https://github.com/RfidResearchGroup/proxmark3.git
cd proxmark3
git fetch --unshallow
git checkout <tag correspondente ao teu firmware>
make client
# resultado em client/proxmark3.exe
```

Ambiente de compilação usado: MSYS2 UCRT64, com os pacotes
`git base-devel mingw-w64-ucrt-x86_64-{gcc,cmake,readline,lua,jansson,lz4,python,bzip2,openssl}`
instalados via `pacman`.
