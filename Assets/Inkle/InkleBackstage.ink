->inicio
=== inicio ===
BIENVENIDO A BACKSTAGEVR
Puedes leer la motivación de este simulador o para poder jugar lee la descripción.
* [Motivación] -> motivacion
* [Descripción] -> descripcion

=== motivacion ===
En la industria del entretenimiento, en especial conciertos masivos, se necesita personal capacitado para cumplir roles especificos para cumplir tareas en concreto, ya sea acomodar sillas o estar pendiente de los ingresantes. Aveces por razones varias se necesita nuevo personal que en muchas ocasiones no tiene experiencia o que no han estado en el lugar próximo al evento. Lo que se quiere lograr es entrenar una gran cantidad de personas lo más rápido posible, buscando que el aprendizaje sea  que en una capacitación real.

+ {not descripcion}[Descripción] -> descripcion
+ {not jugar and descripcion}[Jugar] -> jugar

=== descripcion ===
El simulador se enfoca en entrenar personal para conciertos o eventos. Dentro de este hay dos roles: acomodador y seguridad. El acomodador se encarga de revisar y colocar los intrumentos que se van a usar en el escenario, mientras que el de seguridad asegura las zonas del publico y mantiene el orden.
+ {not motivacion} [Motivación] -> motivacion
+ [Jugar] -> jugar

=== jugar ===
Antes de inciar, hay aspectos básicos del simulador que tienes que saber.
+ [Movimiento] -> movimiento
+ [Interacción] -> interaccion
+ {movimiento and interaccion}[Escoger rol] -> roles


=== roles ===
Alparecer ya sabes los aspectos básicos. Ahora escoge tu rol:
    +[Acomodador] -> acomodador
    +[Seguridad] -> seguridad

=== movimiento ===
Para moverte hay dos formas, puedes usar el joystick del control o hay zonas de teletransporte que puedes usar.
TODO:Poner imagenes o videos
+ {movimiento and interaccion}[Escoger rol] -> roles
+ [Interacción] -> interaccion
+[Volver al menú principal] -> jugar

=== interaccion ===
Para iteractuar con los objetos puedes acercarte a ellos y mantener el gatillo izquierdo o derecho dependiendo de la mano con la que quieres sujetar el objeto.
TODO:Poner imagenes o videos
+ {movimiento and interaccion}[Escoger rol] -> roles
+[Movimiento] -> movimiento
+[Volver al menú principal] -> jugar

=== acomodador ===
El rol de acomodador se encarga de revisar el estado de los intrumentos. Incialmente habran varios tipos de instrumentos. Los que hay que acomodor estaran resaltados. Hay que revisar cada uno a fondo para que no haya defecto alguno.
TODO:Poner imagenes o videos
+[Siguiente]
Luego de revisar los instrumentos, tendras que poner cada uno es su lugar. Se iluminara en el suelo un circulo azul que te indicara donde poner cada intrumento.
TODO:Poner imagenes o videos
++[Siguiente] -> fin
++ [Ver de nuevo] -> acomodador
Cuando termines de colocar los instrumentos te teletransportara a la pantalla del menú.

=== seguridad ===
El rol de seguridad se encarga de organizar las cercas que limita la zona del público. La idea es asegurar la zona lo mejor posible.
+[Siguiente]
Cuando termines de colocar las cercas te teleportaras a la mitad del escenario, mirando al público. Tu objetivo es mantener el orden, es muy probable que las personas se salten las cercas para llegar al escenario.
++ [Siguiente]
Cuando termines la simulación te teletransportara a la pantalla del menú.
+++ [Siguiente]->fin
+++ [Ver de nuevo] -> seguridad
TODO:Poner imagenes o videos



=== fin ===
+[Volver a los roles]-> roles
+[Volver al inicio] -> inicio
-> END