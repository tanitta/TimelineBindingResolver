# TimelineBindingResolver

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://github.com/tanitta/HoudiniReplaceGroupSelectionWithAttribute/blob/main/LICENSE)

# Description

This component stores bindings attached to the PlayableDirector Component as relative path strings.
Using this component, it becomes possible to store reference information from Timeline prefabs to external assets within a prefab,
thereby reducing asset conflicts during team production with tools like Git and preventing binding corruption.

# Install

Unity Package Manager(UPM) support path query parameter of git package.
You can add `https://github.com/tanitta/TimelineBindingResolver.git` to Package Manager or 
add `net.tanitta.timeline_binding_resolver": "https://github.com/tanitta/TimelineBindingResolver.git` to Packages/manifest.json.

# Usage

## Setup

1. Prepare an instance of the prefab that contains a GameObject to which the PlayableDirector Component is attached.
1. Attach the TimelineBindingResolver Component to the GameObject and save the changes to the prefab.

## Save and load

Manage the saving and loading of binding information. Saving must be done manually, but loading is done automatically when the scene is opened.

1. Write the binding information into the TBR Component using relative paths. Execute TBR->Collect from the top left hamburger menu.
1. Apply the changes of this TBR's binding to the prefab.
1. Verify the saved information in the TBR (Optional). Discard the scene changes and reopen the scene to automatically overwrite the binding information written in the TBR into the PlayableDirector Component's binding.

After completing the initial setup, perform the steps under "Setup and Load" each time a new binding is added to the PlayableDirector's Timeline.

# Note

- Currently, TBR's Collect and Apply do not function in the Prefab Editor.
