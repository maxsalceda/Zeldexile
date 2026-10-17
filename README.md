# Zeldexile

Jogo de aventura em mundo aberto 2.5D, com câmera em terceira pessoa, no **padrão de design de Breath of the Wild**:
exploração livre (escalar, planar, nadar), um mundo com física e química próprias, armas que quebram, corações,
roda de stamina, culinária e armaduras que respondem às pressões do mundo. Sem classes, níveis ou loot aleatório.

Design completo: [Docs/GDD.md](Docs/GDD.md) e a Bíblia de Design (link no GDD).

## Abrir
1. Unity Hub → **Add project from disk** → `D:9 - Zeldexile\Zeldexile` (Unity 6000.6.4f1).
2. Abra `Assets/Zeldexile/Scenes/Prototype.unity` e aperte Play.
   Se a cena sumir, recrie pelo menu **Zeldexile → Recriar cena de protótipo**.
3. **Mundo aberto**: `Assets/Zeldexile/Scenes/ValeInicial.unity` é a primeira região (1 km², o Vale dos Pomares).
   Recrie pelo menu **Zeldexile → Recriar cena do Vale Inicial**. O jogo (build) abre nele;
   `Zeldexile.exe -ilha` abre a ilha de protótipo e `-vitrine` a vitrine do herói.
   Detalhes, medições e como expandir: [Docs/Regiao-Vale-Inicial.md](Docs/Regiao-Vale-Inicial.md).

## Controles
| Ação | Teclado/mouse | Controle |
|---|---|---|
| Andar / câmera | WASD / mouse | Analógicos |
| Zoom da câmera (1,2 a 12 m) | Roda do mouse | Analógico direito pressionado + cima/baixo |
| Correr (gasta stamina) | Shift | B / Círculo |
| Agachar / levantar (anda devagar) | Ctrl | Analógico esquerdo pressionado |
| Pular / planar (no ar) / salto de escalada | Espaço | X / Triângulo |
| Escalar | andar contra a parede | — |
| Atacar com a arma | Clique esquerdo | Y / Quadrado |
| Ataque pesado (combo de espada e escudo) | Botão direito (toque) | RT (toque) |
| Arremessar a arma | F | RB |
| Arco (segurar para mirar; no ar = câmera lenta) + atirar | Botão direito segurado + clique | RT segurado + Y |
| Lançar runa / escolher runa | R / 1–4 | LT / Direcional |
| Travar alvo (toque) / escudo erguido (segurar) | Q | LB |
| Defesa perfeita | Espaço com o escudo erguido | X com LB |
| Esquivar (perfeita = Rajada) | Alt ou C (ou Espaço com alvo travado) | X com alvo travado |
| Usar (pegar arma, abrir baú, cozinhar, rezar) | E | A / Cruz |
| Bolsa (armas, arcos, escudos, armaduras, materiais, comida, runas) | Tab | Start |

## O que testar na ilha
- **Acampamento** (início): fogueira com panela (E para cozinhar), Estátua da Deusa (4 orbes = coração ou stamina),
  armas no chão (espada, lança, escudo, arco) e comida para colher. Armas pegas voltam na Lua de Sangue.
- **Prova da Mesa Alta** (sul): alta demais para uma roda. Queime a grama com **Brasa** e plane na corrente de ar.
- **Prova do Lago** (leste): longe demais para nadar. Erga pilares de gelo com **Geada** (só 3 de cada vez).
- **Prova dos Três Pilares** (extremo leste): o portão abre com os três pilares energizados; **Faísca** salta por metais.
- Cada Prova dá um **Orbe Espiritual**, uma arma e materiais. Criaturas deixam cair a arma que carregavam e partes.
- Golpes gastam a arma; o último golpe dobra o dano; arremesso quebra. Esquiva perfeita abre a Rajada.
- Noite (21:00–05:00): criaturas dormem. Dia 5, meia-noite: **Lua de Sangue** (tudo renasce; cozinhar entre 23:30 e 00:15 é crítico).

## Estrutura
```
Zeldexile/Assets/Zeldexile/
  Scripts/Domain    regras puras: elementos, runas, corações, stamina, armas/bolsas, inimigos/escala,
                    materiais/culinária, armaduras, clima, regras de dano
  Scripts/Runtime   Player (movimento, câmera, runas, combate, interação), World (química, água, ar quente,
                    mecanismos, objetos interativos), Combat (vida, projéteis, criaturas), Gameplay (ilha, loot), UI (HUD)
  Scripts/Domain/Regions  mundo aberto em C# puro: setores, dados da região, relevo/água/trilhas, distribuição
  Scripts/Runtime/World/Region  montagem da região: terreno por setor, água, pontes, natureza instanciada, grama
  Resources/World/Regions  dados das regiões (JSON); vale_inicial.json é o primeiro km²
  Scripts/Editor    criação das cenas
  Tests             EditMode (domínio) e PlayMode (gancho no mundo)
```

## Visual
3D low-poly estilizado, polígonos otimizados e arestas chanfradas. Shaders `Zeldexile/Toon` (cenário),
`Zeldexile/ToonOutline` (personagens e props) e `Zeldexile/Water`, em `Assets/Zeldexile/Resources/Shaders`.

- **Relevo**: `Scripts/Runtime/World/Terrain` gera o terreno facetado a partir de `IslandShape.Height`.
- **Modelos**: gerados no Blender 3.6 por `Tools/Blender/build_models.py` (herói, Cascudo, árvores, rochas,
  baú, pilar de cobre, muros, portão, teto, grama). Os `.blend` ficam em `Art/Blender` para edição manual.
  Cada malha se chama `Junta~Peça@slot`: o Unity remonta as juntas e pinta pela cor do slot.
- **Herói do Meshy**: o herói em jogo é `Resources/Models/Skinned/heroi_meshy.fbx` (esqueleto Mixamo, textura
  `heroi_meshy_cor.png`, shader `Zeldexile/ToonOutlineTextured`). `SkinnedHero` conserta as bindposes do FBX do
  Meshy, endireita o esqueleto e copia a pose procedural do `CharacterAnimator` para os ossos; espada, escudo e
  arco vêm do `heroi_anime`. Para trocar o herói, substitua os dois arquivos; para voltar ao modelo do Blender,
  esvazie `RigStyle.Hero.SkinnedModel`.

Para regenerar os modelos:
```
"E:\Steam\steamapps\common\Blender\blender.exe" -b --factory-startup -P Tools/Blender/build_models.py -- "Zeldexile/Assets/Zeldexile/Resources/Models" "Art/Blender"
```
A natureza do mundo aberto (árvores, rochas, plantas, peças de ponte) vem de `Tools/Blender/build_nature.py`,
com o mesmo comando trocando o script e a pasta dos `.blend` para `Art/Blender/Natureza`.
Depois, no Unity: **Zeldexile → Reimportar modelos do Blender**. `Tools/Blender/check_normals.py` acusa faces invertidas num `.blend`.

O teste de Play Mode `VisualPreviewTests` salva prévias em `Zeldexile/Logs/preview-*.png` (ilha, vista geral, lago, portão e folha de poses).

## Testes
Unity → Window → General → Test Runner, ou por linha de comando:
(o domínio puro também compila fora do Unity: `dotnet build Tools/DomainCheck`)
```
Unity.exe -batchmode -projectPath "D:\19 - Zeldexile\Zeldexile" -runTests -testPlatform EditMode
```
