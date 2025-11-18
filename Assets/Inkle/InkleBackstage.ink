-> inicio
=== inicio ===
BIENVENIDO A BACKSTAGEVR
Puedes leer la motivación de este simulador o para poder jugar lee la descripción.
* [Motivación] -> motivacion
* [Descripción] -> descripcion

=== motivacion ===
En la industria del entretenimiento en vivo, especialmente en los conciertos masivos, es fundamental contar con personal capacitado para asumir roles específicos, desde la organización de las sillas y el control de accesos, hasta la atención de los asistentes. Sin embargo, por diversas razones, con frecuencia se debe vincular nuevo personal que no siempre cuenta con experiencia previa en este tipo de eventos. Lo que se busca es entrenar, en el menor tiempo posible, a un gran número de personas, logrando un nivel de aprendizaje y preparación equiparable o mejor al de una capacitación presencial tradicional.

+ {not descripcion}[Descripción] -> descripcion
+ {not jugar and descripcion}[Jugar] -> jugar

=== descripcion ===
El simulador se enfoca en entrenar personal para conciertos o eventos. Dentro de este se simula el rol de acomodador. El acomodador se encarga de revisar el estado actual de los instrumentos y colocarlos segun si se van a usar en el escenario.
+ {not motivacion} [Motivación] -> motivacion
+ [Jugar] -> jugar

=== jugar ===
Antes de inciar, hay aspectos básicos del simulador que tienes que saber.
+ [Movimiento] -> movimiento
+ [Interacción] -> interaccion
+ {movimiento and interaccion}[Rol acomodador] -> roles


=== roles ===
Alparecer ya sabes los aspectos básicos. Ahora veamos que tienes que hacer.
    +[Vamos ha acomodar] -> acomodador

=== movimiento ===
Para moverte existen dos formas: puedes usar el joystick del control izquierdo o con el joystick del control derecho puedes teletranportarte a las zonas verdes demarcadas o al escenario.

+ {movimiento and interaccion}[Rol acomodador] -> roles
+ [Interacción] -> interaccion
+ [Volver al menú principal] -> jugar

=== interaccion ===
Para iteractuar con los objetos puedes acercarte a ellos y mantener el gatillo izquierdo o derecho dependiendo de la mano con la que quieres sujetar el objeto.
+ {movimiento and interaccion}[Rol acomodador] -> roles
+ [Movimiento] -> movimiento
+ [Volver al menú principal] -> jugar

=== acomodador ===
El rol de acomodador se encarga de revisar el estado de los intrumentos. Inicialmente habran varios tipos de instrumentos. Los que hay que acomodar estarán resaltados. Hay que revisar cada uno a fondo para que no haya defecto alguno.
+[Siguiente]
Al revisar los instrumentos te va a salir un menú donde te dira las cosas a revisar del instrumento, además de poder aprobar, reportar o reemplazar el instrumento según lo requiera la situación.
++ [Siguiente]
Luego de revisar los instrumentos, tendras que poner cada uno es su lugar. Se iluminará en el suelo del escenario una luz parpadenate que te indicará donde poner cada intrumento.
+++ [Siguiente]
Cuando finalices de colocar los instrumentos el simulador terminará o tambien puedes darle a terminar juego después de darle a siguiente.
++++ [Siguiente] -> fin
++++ [Ver de nuevo] -> acomodador

=== fin ===
+ [Volver] -> acomodador
+ [Volver al inicio] -> inicio
+ [Terminar juego] -> END