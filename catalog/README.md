# Catálogo partilhável

`catalog.json` é uma lista de jogos já catalogados — processo verificado a vigiar e descrição
traduzida nos 5 idiomas suportados (`fr`, `en`, `pt`, `es`, `it`) — para o Label Studio pré-preencher
automaticamente quando encontra uma correspondência (por `SteamAppId`, `EpicItemId` ou `GogGameId`,
consoante a plataforma). Fica versionado neste repositório, tal como `samples/` — não é obtido por
rede, e não precisa de nada especial para atualizar: edita o ficheiro, faz commit.

## Adicionar uma entrada

Cada objeto no array precisa de `Platform` (`Steam`, `Epic` ou `Gog`), do identificador próprio
dessa plataforma (`SteamAppId`, ou `EpicNamespace`+`EpicItemId`+`EpicAppName`, ou `GogGameId`),
`Title`, `Process`, e `Description` (um objeto com uma chave por idioma — `fr`/`en`/`pt`/`es`/`it`).
`Cover` é opcional.

## Sobre as capas (`Cover`)

O campo `Cover`, quando presente, aponta para um ficheiro de imagem em `catalog/covers/` (ex.:
`"Cover": "inside.jpg"` → `catalog/covers/inside.jpg`). **Esta pasta está vazia de propósito** — não
inclui capas de jogos reais, porque isso significaria guardar arte comercial de terceiros no
histórico do Git, o que não é uma decisão para tomar sem seres tu a rever e a escolher as imagens.
O mecanismo já está pronto: basta lá pores um ficheiro e apontar `Cover` para ele, que o Label
Studio usa-o automaticamente — mais útil ainda para Epic/GOG, que não têm CDN de capas grátis como
a Steam.
