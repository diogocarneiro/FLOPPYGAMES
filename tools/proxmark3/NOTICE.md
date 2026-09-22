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
