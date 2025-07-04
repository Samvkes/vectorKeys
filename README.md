An attempt at a simple tool for sketching out typefaces using only the keyboard (taking a lot inspiration from vim). The goal is to be able to design some letters during a trainride.

Just a note, this uses a gdextension to use a dynamic lib for some of the more complex bezier processing. The lib is written in Rust, so you'll have to do `cargo build` in the 'Bezier_sam' folder. You might also have to adjust the gdextension file depending on your operating system. 

The bezier lib used is the excellent https://graphite.rs/libraries/bezier-rs/.