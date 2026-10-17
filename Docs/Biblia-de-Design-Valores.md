<!-- Exportado da aba "Valores propostos" do Claude Doc https://claude.ai/code/artifact/a37f7bd0-7e67-4e64-8dee-852e05bb0b9a em 17/10/2026. O documento vivo é o Claude Doc; este arquivo é uma cópia para o repositório. -->

# Valores propostos para o jogo

Oct 17, 2026 · @Max

Todos os números abaixo derivam das curvas da aba principal, reescaladas para um jogo menor que Breath of the Wild. As proporções entre tiers, custos e recompensas são mantidas; os totais caem.

## Escopo e premissas

O jogo é dimensionado para 20 a 30 horas de campanha (BotW: 50 a 60 h), com um único mapa contínuo de 5 regiões e 3 masmorras principais. Toda razão entre tiers (×3 em HP, ×1,4 em ataque de arma, 4 orbs por upgrade) é copiada; o que muda é o total.

| Dimensão | Breath of the Wild | Proposto | Razão |
| --- | --- | --- | --- |
| Duração da campanha | 50 a 60 h | 20 a 30 h | 0,45 |
| Regiões de torre | 15 | 5 | 0,33 |
| Santuários | 120 | 80 | 0,67 |
| Masmorras principais | 4 + castelo | 3 + fortaleza final | 0,75 |
| Sementes Korok | 900 | 300 | 0,33 |
| Corações máx. | 30 | 20 | 0,67 |
| Rodas de stamina máx. | 3 | 3 | 1,0 |
| Famílias de inimigo comum | 8 | 4 | 0,5 |
| Inimigos de elite | 4 tipos | 3 tipos | 0,75 |
| Sets de armadura | 15 | 8 | 0,53 |
| Dia de jogo | 24 min reais | 24 min reais | 1,0 |

**Premissas de ajuste:**

- Menos santuários, mas cada um dá 1 orb e cada upgrade custa 4 orbs, igual a BotW; o orçamento total de orbs é verificado na última seção.
- Tiers de inimigo mantêm ×3 de HP e o jogador mantém a mesma quantidade de golpes para matar (2 a 10) em cada fase da curva.
- Preços em rupees caem para \~0,6 dos de BotW porque há menos materiais para vender num mapa menor.
- Durações de comida, recargas de runa e relógio ficam idênticos: são tempo real e não dependem do tamanho do mapa.

## Jogador

O jogador começa com 3 corações e 1 roda e termina, no máximo, com 20 corações ou 3 rodas, nunca os dois.

**Vida e stamina**

| Parâmetro | Valor |
| --- | --- |
| Corações iniciais / máximos | 3 / 20 (1 coração = 4 pontos de vida; máx. 80 pontos) |
| Heart Container | 4 Spirit Orbs; 17 containers para o máximo |
| Roda de stamina | 1000 unidades por roda; 1 Stamina Vessel = 200 (1/5 de roda); 10 vessels para 3 rodas |
| Regeneração | Começa 0,5 s após parar de gastar; 300 unidades/s (roda cheia em \~3,3 s) |
| Corações amarelos (Hearty) | Até +20 temporários; somem ao levar dano, não regeneram |
| Stamina amarela (Enduring) | Até +2 rodas temporárias; some ao ser gasta |
| Troca na Estátua | Vende por 100, compra por 120 |

**Custo de stamina por ação (unidades; roda = 1000)**

| Ação | Custo | Tempo para esvaziar 1 roda |
| --- | --- | --- |
| Corrida | 110/s | \~9 s |
| Escalada | 70/s | \~14 s |
| Salto na escalada | 150 fixo | 6 saltos |
| Escalada na chuva | 70/s + escorrega a cada 3 passos | — |
| Nado | 60/s | \~17 s |
| Dash na água | 200 fixo | 5 dashes |
| Paraglider | 25/s | 40 s |
| Ataque carregado 1 mão (giro) | 150 fixo por giro | — |
| Ataque carregado 2 mãos (segurar) | 80/s | \~12 s |
| Câmera lenta com arco no ar | 250/s | 4 s |
| Esporear cavalo | 1 esporada (gauge do cavalo, não do jogador) | — |

Stamina zerada: escalando, cai; nadando, afoga (perde 1 coração, volta à margem); planando, fecha e cai; correndo, para ofegante por 2 s. Comparativo com BotW: valores escolhidos para reproduzir as mesmas durações percebidas (corrida \~8 a 9 s, planar \~40 s).

No protótipo atual (ilha de 180 m, Mesa Alta de 26 m), o código usa escalada 95/s a 2 m/s e nado 130/s a 2,4 m/s, para que a mesa e o lago continuem fora do alcance de uma roda; os valores de 70/s e 60/s acima são o alvo para o mapa final, quando o terreno for redimensionado.

**Fórmula de dano recebido**

```latex
\text{pontos perdidos} = \max\left(1,\ \text{ataque do inimigo} - \text{defesa total}\right)
```

Ataque e defesa em pontos de vida (1 coração = 4). Defesa total = soma das 3 peças de armadura + bônus Tough (nível 1: +4, nível 2: +8, nível 3: +12). Dano mínimo é sempre 1/4 de coração.

**Dano causado**

```latex
\text{dano} = \text{ataque da arma} \times \text{mult. de golpe} \times \text{mult. de estado} + \text{bônus Mighty}
```

| Multiplicador | Valor |
| --- | --- |
| Golpe normal | 1,0 |
| Último golpe antes de quebrar | 2,0 |
| Arremesso | 1,0 (2,0 se a arma estava vermelha) |
| Ataque furtivo pelas costas | 8,0 |
| Headshot com flecha | 2,0 |
| Inimigo congelado | 3,0 (e descongela) |
| Inimigo paralisado | 1,0 (mas não reage) |
| Mighty nível 1 / 2 / 3 | +20 % / +30 % / +50 % |

**Queda:** sem dano até 6 m; de 6 a 12 m perde 1 coração; acima de 12 m perde 1 coração por 3 m extras; acima de 30 m é morte, salvo água funda (sem dano) ou paraglider.

## Mundo

Cinco regiões, cada uma com uma torre, 16 santuários e um bioma de pressão; o mapa inteiro abre ao fim do platô inicial, que ocupa 1 h 30 min a 2 h.

**Conteúdo por região e total**

| Elemento | Por região | Total | BotW (total) |
| --- | --- | --- | --- |
| Torre | 1 | 5 | 15 |
| Santuários | 16 | 80 | 120 |
| Santuários escondidos (Shrine Quest) | 5 | 25 | 42 |
| Sementes Korok | 60 | 300 | 900 |
| Estábulo | 1 | 5 | 15 |
| Vila (uma raça) | 1 | 5 | 9 |
| Acampamentos inimigos | 5 | 24 (+ fortaleza) | \~130 |
| Inimigo de elite | 1 a 2 | 8 | \~40 Lynels |
| Great Fairy | 1 (em 4 regiões) | 4 | 4 |
| Memórias | — | 8 | 13 |
| Side quests | 8 | 40 | 76 |
| Masmorra principal | 1 (em 3 regiões) | 3 + fortaleza final | 4 + castelo |

**Platô inicial:** 4 santuários (4 runas), 1 torre, 1 acampamento, 2 Koroks, frio na parte alta (ensina roupa ou comida), paraglider ao fim. Duração alvo 90 a 120 min.

**Densidade e distâncias**

| Parâmetro | Valor |
| --- | --- |
| Lado do mapa | \~4 km × 3 km (BotW \~9 × 7 km) |
| Velocidade a pé / correndo / cavalo galope | 4 m/s / 7 m/s / 14 m/s |
| Travessia a pé de ponta a ponta | \~15 min (BotW \~35 min) |
| Tempo máximo sem algo novo no campo de visão | 60 a 90 s a pé |
| Distância média entre santuários | 350 a 450 m |
| Distância mín. entre torres | 1,2 km |
| Raio de revelação de mapa por torre | Região inteira (\~1,5 km de raio) |
| Alcance de leitura de torre / santuário / acampamento | Mapa inteiro / 600 m / 150 m |
| Pins de mapa / beacons | 60 / 5 |

**Relógio:** 1 min real = 1 h de jogo; dia de 24 min. Noite das 21:00 às 05:00 (8 min). Chuva: 25 % dos dias por região (exceto região chuvosa fixa: 60 %, e deserto: 0 %). Tempestade com raios: 1 em 4 chuvas.

**Inventário por sementes Korok (Hestu equivalente)**

| Tipo | Slots iniciais → máx. | Custo por slot (sementes) | Total |
| --- | --- | --- | --- |
| Armas | 6 → 16 | 1, 2, 3, 4, 6, 8, 10, 12, 15, 18 | 79 |
| Arcos | 4 → 10 | 1, 2, 3, 4, 6, 8 | 24 |
| Escudos | 3 → 14 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 | 67 |
| Soma |  |  | 170 de 300 (57 %; BotW 49 %) |

As 130 sementes restantes são recompensa de completismo (item cosmético ao entregar 300), como em BotW.

## Armas, escudos, arcos e inventário

Seis tiers de arma, com ataque crescendo \~1,4× por tier e durabilidade \~1,25×; duas exceções deliberadas (vidro e âncora) por classe.

**Curva base (armas de uma mão)**

| Tier | Nome de exemplo | Ataque | Durabilidade | Quem porta | Quando aparece |
| --- | --- | --- | --- | --- | --- |
| 0 | Galho, clava enferrujada | 4 | 8 | Grunt vermelho | Platô |
| 1 | Espada de viajante | 10 | 20 | Grunt vermelho, brutes vermelhos | Platô e 1ª região |
| 2 | Espada de soldado | 18 | 26 | Tier azul | Após 1ª masmorra ou 60 pts de escala |
| 3 | Espada de cavaleiro | 26 | 32 | Tier preto | Após 2ª masmorra ou 180 pts |
| 4 | Espada real | 36 | 40 | Tier prata, elites | Após 3ª masmorra ou 400 pts |
| 5 | Lâmina ancestral | 45 | 50 | Laboratório (troca por peças de sentinela), sentinelas | Qualquer momento, com material |
| Vidro | Espada da guarda real | 48 | 12 | Fortaleza final | — |
| Âncora | Espada lendária | 28 (56 contra corrupção) | 40, recarrega em 8 min | Requer 10 corações | — |

**Multiplicadores por classe (sobre a curva base)**

| Classe | Ataque | Durabilidade | Golpes por combo | Flurry Rush |
| --- | --- | --- | --- | --- |
| Uma mão | ×1,0 | ×1,0 | 4 | 7 |
| Duas mãos | ×1,5 | ×1,1 | 3 | 4 |
| Lança | ×0,7 | ×1,3 | 5 | 9 |
| Arremesso (qualquer) | ×1,0 (×2,0 se vermelha) | Quebra ao acertar inimigo | — | — |

**Arcos**

| Tier | Ataque | Durabilidade | Tiros | Alcance útil |
| --- | --- | --- | --- | --- |
| 0 | 4 | 20 | 1 | 20 m |
| 1 | 8 | 30 | 1 | 25 m |
| 2 | 14 | 40 | 1 | 30 m |
| 3 | 22 | 55 | 1 | 35 m |
| 4 | 30 | 70 | 1 ou 3 (multi-shot) | 40 m |
| 5 (ancestral) | 40 | 90 | 1, tiro rápido | 50 m |

Flechas: normal 1× ataque do arco; fogo +10 fixo e incendeia; gelo +10 e congela; raio +20 e paralisa 3 s; bomba 30 em raio 3 m; ancestral 50 e ×5 contra sentinelas.

**Escudos**

| Tier | Defesa (guard) | Durabilidade | Surf |
| --- | --- | --- | --- |
| 0 (tampa, madeira) | 10 | 10 | Lento |
| 1 | 20 | 20 | Normal |
| 2 | 35 | 35 | Normal |
| 3 | 50 | 50 | Rápido |
| 4 | 65 | 70 | Rápido |
| Âncora (escudo lendário) | 80 | 600 | Rápido |

Bloqueio: se o ataque do inimigo for menor que a defesa do escudo, zero dano e −1 durabilidade; se maior, o jogador perde (ataque − defesa) pontos e o escudo perde 2. Parry perfeito: zero dano, zero durabilidade, inimigo atordoado 2 s. Surf: −1 durabilidade a cada 3 s em pedra, zero em neve e areia.

**Modificadores** (só em armas do mundo, nunca de loja): Attack Up +3 a +8 (azul) / +9 a +15 (amarelo); Durability Up +5 a +10 / +11 a +20; Critical Hit (último golpe do combo ×2); Long Throw ×1,5 alcance; Quick Shot; Multi-Shot ×3; Shield Guard Up +10 / +20. Chance de modificador aparece a partir de 120 pontos de escala, igual à lógica de BotW.

## Inimigos

Quatro famílias comuns com quatro tiers (×3 de HP por tier), três elites, quatro chefes e uma escala de dificuldade que promove tiers conforme o jogador mata.

**HP por família e tier** (ataque em pontos de vida; 4 pontos = 1 coração)

| Família (papel BotW equivalente) | Vermelho | Azul | Preto | Prata | Ataque por tier (arma típica) |
| --- | --- | --- | --- | --- | --- |
| Grunt (Bokoblin) | 12 | 40 | 130 | 400 | 4 / 10 / 20 / 36 |
| Brute (Moblin) | 45 | 120 | 300 | 900 | 8 / 16 / 28 / 48 |
| Ágil (Lizalfos; variantes fogo/gelo/raio no tier preto) | 40 | 100 | 240 | 720 | 6 / 12 / 24 / 40 |
| Scout de santuário (I / II / III / IV) | 10 | 80 | 1200 | 2400 | 4 / 12 / 24 / 40 |
| Esqueleto noturno (Stal) | 1 (remonta 3×) | — | — | — | Igual ao grunt vermelho |
| Voadores / gel / emboscada (Keese, Chuchu, Octorok) | 1 / 3–20–48 / 8 | — | — | — | 2 / 2–4–8 / 4 |

**Elites e gigantes**

| Tipo (equivalente) | Tiers | HP | Ataque | Onde |
| --- | --- | --- | --- | --- |
| Elite (Lynel) | vermelho / azul / prata | 1500 / 2250 / 3000 | 24 / 36 / 48 | 1 por região + 3 na fortaleza |
| Gigante (Hinox) | vermelho / azul / preto | 500 / 700 / 900 | 20 / 26 / 32 | 1 por região |
| Golem (Talus) | comum / elemental / raro | 250 / 600 / 750 | 16 / 24 / 24 | 2 por região |
| Sentinela móvel (Guardian Stalker) | único | 1200 | Laser 60 (one-shot até 15 corações sem defesa); pernas 80 cada | Ruínas e fortaleza |
| Sentinela fixa (Decayed) | único | 400 | Laser 60 | Campos e estradas |

**Chefes**

| Chefe | HP | Ataque | Regra |
| --- | --- | --- | --- |
| Chefe de masmorra (Blight) 1º / 2º / 3º enfrentado | 600 / 900 / 1200 | 16 / 24 / 32 | +300 HP por chefe já vencido, igual à lógica de BotW |
| Chefe de masmorra refeito na fortaleza | 1200 | 32 | Se o jogador pulou a masmorra |
| Líder do clã inimigo (Kohga) | 300 | 12 | Puzzle, não DPS |
| Chefe final, fase 1 | 6000 (−1500 por chefe de masmorra vencido; mín. 1500) | 24 a 40 | Arena |
| Chefe final, fase 2 | 8 pontos fracos × 1 | 20 | Campo aberto a cavalo, arco de luz |

**Pontos de escala de dificuldade** (cada espécie conta até 10 mortes)

| Morte de | Pontos |
| --- | --- |
| Grunt / ágil / brute (qualquer tier) | 1 / 2 / 2 |
| Gigante / golem | 6 |
| Elite | 10 |
| Sentinela móvel | 8 |
| Chefe de masmorra | 20 |

| Total de pontos | Efeito |
| --- | --- |
| 60 | Spawns vermelhos viram azuis; armas tier 1 → 2 |
| 120 | Modificadores de arma começam a aparecer |
| 180 | Azuis viram pretos; armas tier 2 → 3 |
| 400 | Pretos viram prata; armas tier 3 → 4 |

Teto prático: 4 famílias × 10 mortes × \~2 pts + elites e chefes ≈ 420, então o mundo chega ao prata só para quem limpa tudo.

**Lua de Sangue:** a cada 5 dias de jogo (2 h reais jogadas, sem contar menus), à meia-noite. Renasce inimigos e armas do mundo; não renasce materiais nem baús. Janela de crítico garantido na culinária: 23:30 a 00:15.

**Percepção padrão:** cone de visão 90° × 40 m de dia (25 m à noite); audição 12 m andando, 25 m correndo, 4 m agachado (chuva reduz à metade). Corneta acorda todos num raio de 60 m.

## Runas e habilidades de aliado

As quatro runas chegam no platô inicial com os mesmos números de BotW; as três habilidades de aliado (uma por masmorra) seguem a lógica usos/recarga.

**Runas**

| Runa | Dano / efeito | Alcance | Duração | Recarga | Upgrade (laboratório, 3 peças de sentinela) |
| --- | --- | --- | --- | --- | --- |
| Bomba (redonda e cúbica) | 12, raio 3 m; arremesso \~8 m | Detona a até 40 m | Até detonar ou 60 s | 6 s | Bomba+: 24 de dano, recarga 3 s |
| Stasis | Congela 1 objeto; cada golpe acumula ataque da arma × 0,2 m/s de lançamento (máx. 6 golpes) | 25 m | 5 s | 10 s | Stasis+: funciona em inimigos (3 s; elites e chefes 1,5 s), recarga 12 s |
| Magnesis | Move objeto metálico; massa até 10× a do jogador; objeto solto do alto causa dano = massa × 2 | 20 m | Ilimitada | 0 | Nenhum |
| Cryonis | Pilar de gelo 3 m de altura em água; até 3 ativos; o 4º apaga o mais antigo; levanta objetos e inimigos de até 2× a massa do jogador | 20 m | Ilimitada | 0 | Nenhum |
| Câmera e compendium | 200 entradas (BotW 385) | 30 m | — | — | Sensor+: rastreia qualquer entrada |
| Sensor de santuário | Apita até 250 m, intervalo cai com a distância | 250 m | — | — | — |

**Habilidades de aliado** (recarga em minutos reais; refeita 3× mais rápido dentro da fortaleza final, como em BotW)

| Habilidade (equivalente) | Masmorra | Efeito | Usos | Recarga |
| --- | --- | --- | --- | --- |
| Rajada ascendente (Revali's Gale) | 1ª | Coluna de vento de 40 m a partir de qualquer chão | 3 | 6 min |
| Guarda (Daruk's Protection) | 2ª | Anula um golpe e repele com 4 pts de dano em raio 3 m; segurando o botão de escudo | 3 | 15 min |
| Renascer (Mipha's Grace) | 3ª | Ao morrer: vida cheia + 5 corações amarelos | 1 | 20 min |

A quarta habilidade de BotW (raio em área) não entra: o jogo tem 3 masmorras, e o papel ofensivo em área já é coberto por bombas e flechas de raio. Se uma quarta masmorra for adicionada, a habilidade é raio em raio 8 m, 60 de dano, 3 usos, 12 min.

## Culinária, temperatura e clima

Cada ingrediente carrega três números (cura, potência de efeito, tipo de efeito); o prato soma, dobra a cura por estar cozido e escolhe o nível do efeito pela potência somada.

**Cura por ingrediente** (pontos de vida; 4 = 1 coração; cru = metade)

| Classe de ingrediente | Cura crua | Cura cozida (por unidade no prato) | Exemplos |
| --- | --- | --- | --- |
| Comum (fruta, cogumelo, erva) | 2 | 4 | Maçã, cogumelo básico |
| Carne / peixe | 4 | 8 | Carne crua, peixe comum |
| Premium | 6 | 12 | Carne de primeira, peixe grande |
| Gourmet | 12 | 24 | Carne gourmet |
| Hearty | Cura total | Cura total + 4 amarelos por unidade (máx. +20) | Durian equivalente, trufa grande |

Prato de 5 carnes gourmet = 120 pontos = 30 corações (cura total em qualquer build). Prato de 5 comuns = 20 pontos = 5 corações.

**Níveis de efeito pela potência somada** (ingrediente comum = 1, raro = 2, excepcional = 3)

| Potência somada | Nível | Exemplo |
| --- | --- | --- |
| 1 a 2 | 1 | 1 pimenta |
| 3 a 5 | 2 | 3 pimentas ou 1 pimenta + 1 flor rara |
| 6 ou mais | 3 (máx. 2 para resistências térmicas) | 5 ingredientes raros |

**Durações** (base + 0:30 por ingrediente de efeito extra + 1:00 por ingrediente de duração; crítico +5:00; máx. 30:00)

| Efeito | Base | Valor por nível |
| --- | --- | --- |
| Spicy / Chilly | 2:30 | Anula frio/calor nível 1 / nível 2 |
| Electro | 2:30 | Dano elétrico ×0,5 / ×0,25 / imune a paralisia |
| Fireproof (elixir) | 2:30 | Imune a pegar fogo (nível único) |
| Sneaky | 2:00 | Ruído ×0,7 / ×0,5 / ×0,3 |
| Hasty | 1:00 | Velocidade +10 % / +20 % / +30 % |
| Mighty | 0:50 | Dano +20 % / +30 % / +50 % |
| Tough | 0:50 | Defesa +4 / +8 / +12 |
| Energizing | Instantâneo | Recupera 1/2 / 1 / 2 rodas |
| Enduring | Instantâneo | +1/2 / +1 / +2 rodas amarelas |

**Regras fixas:** até 5 ingredientes; dois tipos de efeito no mesmo prato se anulam; elixir = 1+ critter + 1+ parte de monstro (sem cura, só efeito); comida + parte de monstro = Dubious Food (4 pontos); minério ou madeira = Rock-Hard (1 ponto); crítico aleatório 10 % (+1 nível, +5:00 ou +12 pontos), 100 % na janela da Lua de Sangue; parte de dragão equivalente: +5:00 garantido (escama), +10:00 (garra), +20:00 (chifre).

**Temperatura**

| Zona | Gatilho | Dano | Contramedida nível 1 / nível 2 |
| --- | --- | --- | --- |
| Frio 1 | < 0 °C | 1 ponto a cada 5 s | Casaco inicial ou Spicy 1 / set de frio ou Spicy 2 |
| Frio 2 | < −10 °C | 1 ponto a cada 2,5 s | Só nível 2 |
| Calor 1 | > 40 °C | 1 ponto a cada 5 s | Roupa de deserto ou Chilly 1 |
| Calor 2 | > 50 °C | 1 ponto a cada 2,5 s | Só nível 2 |
| Escaldante | Região vulcânica | Pega fogo: 1 ponto a cada 0,5 s + queima armas de madeira | Set à prova de fogo ou elixir Fireproof |
| Água gelada | Lagos de neve | 1 ponto por segundo, sem proteção | Cryonis ou contornar |
| Raio | Tempestade, metal equipado | 20 pontos (5 corações) e derruba o metal; aviso de 5 s com faíscas | Desequipar metal; usar contra inimigos |

Temperatura por região: campo 15 a 28 °C (nunca pune); montanha −12 °C à noite a −2 °C ao sol; deserto 48 °C de dia e −5 °C à noite; vulcão 55 °C e escaldante no cume; fogueira acesa anula frio 1 num raio de 3 m.

## Armaduras e economia

Oito sets, cada um resolve uma pressão do mundo; a defesa por peça segue a curva de BotW (3 → 5 → 8 → 12 → 20) e os preços ficam em \~0,6 dos originais.

**Defesa por peça e nível de upgrade**

| Defesa base | Nível 1 | Nível 2 (bônus de set) | Nível 3 | Nível 4 |
| --- | --- | --- | --- | --- |
| 1 (roupa inicial, joias) | 2 | 3 | 4 | 6 |
| 3 (sets normais) | 5 | 8 | 12 | 20 |
| 4 (set pesado, ancestral) | 7 | 12 | 18 | 28 |

Set normal completo: 9 na base, 24 no nível 2, 60 no nível 4. Contra ataque 36 (prata), o jogador no nível 4 leva 1 ponto por golpe; na base, 27 pontos (quase 7 corações). Essa amplitude é a mesma de BotW e é intencional.

**Custo de upgrade por peça** (nas Great Fairies; 4 fadas, desbloqueio 100 / 500 / 1000 / 5000 rupees)

| Nível | Materiais |
| --- | --- |
| 1 | 3 materiais comuns do tema do set |
| 2 | 5 comuns + 3 partes de monstro tier azul |
| 3 | 5 raros + 5 partes tier preto |
| 4 | 8 raros + 3 partes de elite ou 1 parte de dragão |

**Sets**

| Set (equivalente) | Defesa base / peça | Efeito | Bônus de set (nível 2+) | Preço das 3 peças |
| --- | --- | --- | --- | --- |
| Básico (Hylian) | 3 | Nenhum | Nenhum | 60 / 120 / 90 |
| Furtivo (Stealth) | 2 | Ruído ×0,7 por peça | Velocidade noturna +20 % | 300 / 400 / 300 |
| Frio (Snowquill) | 3 | Frio nível 1 por peça (2 peças = nível 2) | Não congela | 400 / 420 / 330 |
| Deserto (Desert Voe) | 3 | Calor nível 1 por peça | Resistência a raio | 400 / 800 / 600 |
| Fogo (Flamebreaker) | 3 | Escaldante nível 1 por peça | Fireproof total | 400 / 360 / 500 |
| Nado (Zora) | 3 | Nado +30 %, sobe cachoeira | Dash aquático ×0,5 stamina | Quest da masmorra |
| Escalada (Climber's) | 3 | Escalada +20 % por peça | Stamina de escalada ×0,7 | 3 santuários escondidos |
| Ancestral (Ancient) | 4 | Dano de sentinela ×0,8 por peça | Armas ancestrais +50 % dano e durabilidade | 1000 / 1200 / 1000 em peças de sentinela + rupees |

Avulsos: 6 joias Gerudo equivalentes (frio, calor, raio, sentinela, nado, defesa) a 300 a 900 rupees + 1 gema; 3 máscaras de disfarce (grunt, brute, elite) por partes de monstro; botas de areia e de neve por quest.

**Preços e sumidouros**

| Item | Rupees |
| --- | --- |
| 5 flechas / 5 de fogo / 5 de gelo / 5 de raio / 1 bomba / 1 ancestral | 20 / 60 / 60 / 80 / 50 / 300 (ancestral só no laboratório, + peças) |
| Registro de cavalo / cama comum / cama macia | 20 / 20 / 40 |
| Casa própria / móvel | 2000 + 20 madeiras / 100 cada |
| Vila construída (Tarrey Town equivalente) | 4 etapas, 10 / 20 / 30 / 50 madeiras |
| Ressuscitar cavalo | 600 |
| Upgrade de runa | 3 peças de sentinela cada |

**Venda** (compra de loja = 2× a venda): material comum 2 a 5; carne 10; gourmet 35; parte de monstro vermelho 5, azul 15, preto 40, prata 100, elite 150; âmbar 20, topaz 120, rubi 150, safira 160, diamante 300; pepita de ouro 400.

**Orçamento:** um jogador que completa 70 % do jogo deve passar por \~45 000 rupees (BotW \~100 000). Sumidouros somados (todos os sets + 4 fadas + casa + joias) ≈ 32 000, então completar tudo é possível sem grind, mas não sem vender.

## Verificação cruzada

As curvas fecham: em cada fase o jogador mata inimigos comuns em 2 a 12 golpes, morre em 2 a 4 golpes sem armadura e só fica "imune" com armadura nível 4, que custa partes de elite.

**Golpes para matar** (arma de uma mão do tier da fase; duas mãos entre parênteses)

| Fase | Arma (ataque) | Grunt | Brute | Ágil | Elite vermelho | Chefe de masmorra |
| --- | --- | --- | --- | --- | --- | --- |
| Platô | Tier 1 (10) | 2 | 5 (3) | 4 (3) | 150 (fugir) | — |
| 1ª masmorra | Tier 2 (18) | 3 | 7 (5) | 6 (4) | 84 (56) | 34 (23) |
| 2ª masmorra | Tier 3 (26) | 5 | 12 (8) | 10 (7) | 58 (39) | 35 (24) |
| 3ª masmorra | Tier 4 (36) | 12 (8) | 25 (17) | 20 (14) | 42 (28) | 34 (23) |
| Fortaleza | Tier 5 (45) | 9 (6) | 20 (14) | 16 (11) | 34 (23) | Final 6000 → 1500: 34 |

Comparação: BotW mata Bokoblin prata com Royal Broadsword em 20 golpes e Lynel vermelho em \~60; aqui são 12 e 42. O jogo é \~30 % mais rápido no combate, coerente com a campanha menor. Flurry Rush (7 golpes) e último golpe ×2 não entram na conta.

**Golpes para morrer** (sem Tough; dano mínimo 1)

| Fase | Vida | Defesa | Vs. grunt da fase | Vs. brute da fase | Vs. elite | Vs. laser 60 |
| --- | --- | --- | --- | --- | --- | --- |
| Platô | 3 corações (12) | 0 a 2 | 3 | 2 | 1 | 1 |
| 1ª masmorra | 7 (28) | 9 (set base) | 28 (dano 1) | 4 | 2 | 1 |
| 2ª masmorra | 11 (44) | 24 (nível 2) | 44 | 11 | 4 | 2 |
| 3ª masmorra | 15 (60) | 36 (nível 3) | 60 | 5 | 5 | 3 |
| Fortaleza | 20 (80) | 60 (nível 4) | 80 | 80 | 80 | 2 (dano fixo de 60, ver nota) |

Nota: o laser de sentinela ignora armadura normal (só o set ancestral reduz, ×0,8 por peça) para preservar o terror do Guardian em todo o jogo, como em BotW. Grunts do tier da fase viram dano 1 assim que o set está no nível da fase; a ameaça real em cada fase é o brute, o elite e o grupo.

**Orçamento de Spirit Orbs**

| Fonte | Upgrades |
| --- | --- |
| 80 santuários ÷ 4 | 20 |
| 3 masmorras | 3 containers |
| Total | 23 |
| Necessário para o máximo dos dois | 17 containers + 10 vessels = 27 |
| Cobertura | 85 % (BotW: 34 de 37 = 92 %) |

Builds possíveis com 100 %: 20 corações + 2,2 rodas; ou 3 rodas + 16 corações; ou 15 corações + 3 rodas com 1 vessel sobrando. A Espada lendária (10 corações) fica alcançável com 7 containers = 28 santuários, ou seja, por volta da 2ª masmorra.

**Outros fechamentos**

- Sementes: 170 de 300 para o inventário máximo (57 %); BotW 49 %.
- Escala de dificuldade: teto prático \~420 pontos contra limiar de 400 para prata; só o completista vê o mundo prateado.
- Economia: \~45 000 ganhos contra \~32 000 de sumidouros; sobra para flechas e camas.
- Stamina: 1 roda esvazia em 9 s de corrida e 14 s de escalada; 3 rodas dão 42 s de escalada, suficiente para o paredão mais alto (\~80 m a 2 m/s) com um salto.
- Relógio e durações: comida de 30:00 cobre 1,25 dias de jogo, igual a BotW.

**O que só o playtest decide:** tempo real de travessia entre santuários (alvo 2 a 3 min), taxa de crítico de culinária (10 %), aviso do raio (5 s) e se o laser de 60 deve cair para 48 quando o jogador tem menos de 6 corações.
