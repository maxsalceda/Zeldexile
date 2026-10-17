# Instruções para agentes

## Toda alteração vai para o executável

O dono do projeto testa direto no jogo, não no editor. **Depois de qualquer alteração no jogo** (código, shader,
modelo, textura, cena, configuração), antes de encerrar a tarefa:

1. Gere o build Windows em `Builds/Windows/Zeldexile.exe`. Isso só funciona com o projeto **fechado** no Unity
   Editor; se ele estiver aberto (existe `Zeldexile/Temp/UnityLockfile`), peça para fechar ou use o menu
   **Zeldexile → Gerar build Windows (cenas salvas)**.
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\19 - Zeldexile\Zeldexile" -executeMethod Zeldexile.EditorTools.WindowsBuild.BuildFromSavedScenes -logFile "D:\19 - Zeldexile\Zeldexile\Logs\build.log"
   ```
   Use `WindowsBuild.Build` (regenera a ilha de protótipo) só quando a mudança exigir recriar a cena.
2. Confira que o build abriu e renderizou: rode o executável com captura e olhe a imagem.
   ```
   "D:\19 - Zeldexile\Builds\Windows\Zeldexile.exe" -zxshot "D:\19 - Zeldexile\Builds\Windows\shot-vale.png" -logFile "D:\19 - Zeldexile\Builds\Windows\log-vale.log"
   ```
   `-ilha` abre a ilha de protótipo e `-vitrine` a vitrine do herói (úteis para ver mudanças no personagem).
   Para mudanças de animação, rode também com `-zxwalk`: o jogo salva o ataque pesado visto de lado
   (`<nome>-pesado-1..6.png`), depois o herói anda sozinho e salva `<nome>-andar-1..6.png`,
   `<nome>-parado.png` e `<nome>-correr.png`. Os quadros de andar precisam mostrar
   pernas em posições diferentes: os testes rodam no editor, e só o executável revela o que o editor esconde
   (por exemplo, `AnimationClip.SampleAnimation` não move ossos fora do editor).
   ```
   "D:\19 - Zeldexile\Builds\Windows\Zeldexile.exe" -zxshot "D:\19 - Zeldexile\Builds\Windows\walk.png" -zxwalk
   ```
3. Diga ao usuário que o executável foi atualizado e o que testar nele. Se o build falhar, informe o erro do
   `Logs/build.log`; não encerre dizendo que está pronto.

`Builds/` está no `.gitignore`: o executável não vai para o git.
