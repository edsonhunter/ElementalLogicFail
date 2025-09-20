How to Play:
- Open the Bootstrapper Scene
- Press Play on Unity Editor
- After the Game Load, press "Play Game" button on the Main Menu
- After the Gameplay Scene loads check the elements wandering the scene
- Increase or decrease the spawn rate through the buttons to add more or less elements
- Check the memory reporter in the middle of the screen


12/09
- Created the project.
- I've decided to try out the "Projects" feature Github provides to organize tasks
- Decided some game design stuffs and where should I start coding.

13/09 
- Create project, folder structures, asmdefs
- I've added the asmdefs first to ensure all dependencies will be respect from the beginning
- Add the first interface and domain to create a behavior test
- With the test up and working, all domains will have tests as part of its creation.
- I decided to use DOTS/ECS as the base for the first prototype because of the nature of the game
- I implemented the spawner authoring/baker/system and elements component/authoring/baker
- I mplemented wandering system
- Stil has some pendings, will work on them tomorrow

14/09
- Today I'll create the collision system and the memory usage analiser.
- Implemented collision systems.
- Fixed a lot of things. My collision system used physics event and interated over it. That's not how NativeStream works.
- Also removed explicit call for LocalTransform to rely only on TransformUsageFlags
- Now I'll work to remove singletons that are impeding me to use multiple spawners

15/09
- I fixed a lot of issues with collisions today.
- Learned how collisions events works for entities
- imported a custom library with collision data and rigidBody data compoenents for entities
- Divided the spawner into two separeted systems
- Learned how systems works under the hood by Unity
- Leraned how jobs and requests were added to buffer than read from it.
- I thought I would start the system architecture but instead I played a little longer with DOTS

16/09
- Today I finished the entire game flow (spawn elements -> elements wander around the map -> elements collide and destroy eachother or create a new one) using only ECS
- Also added a very simple GC memory allocator reader.
- Started doing Unit Tests for the systems
- Fixed a lot of issues with the wandering system, the exponetial creation of prefabs when two collide and learned how systems interact with each other.
- After I finish the UnitTests for the systems I'll finally build the core architecture

17/09
- I have the intention to create the core architecture for the project
- The main tasks I'll try to reach today will be dependency injector for GameServices, SceneManager, Loader, and everything except the game design.
- Ended up not doing the main structure for the project.
- I've tried to add a pooling system to reutilize the objects but the boiler plate + completixy skyrocket.
- Refactor UnitTest to reflect the changes in Collision and PoolSystem

18/09
- Add the core structure to the project
- Create the applicationManager and the dependency injector container
- Apply more fixes to the pooling system

19/08
- Finished the main structure for the project
- Fixed and finalized the PoolSystem for element prefabs
- Created and finished the particle systems
- Added more tests to embrace the new systems
- Finish settings and memory reporter
- Improve camera and fix a lot of things
- Added spawning rate controllers
