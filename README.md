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
