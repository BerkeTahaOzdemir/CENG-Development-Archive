cmake_minimum_required(VERSION 3.10)

project(FileCarvingTool VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(file_carver
    src/main.cpp
)

if(WIN32)
    add_executable(file_carver_gui WIN32
        src/gui_main.cpp
    )

    target_link_libraries(file_carver_gui
        PRIVATE
            comdlg32
            shell32
            ole32
    )
endif()
