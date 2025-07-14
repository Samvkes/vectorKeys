use std::ops::Range;
use std::ops::Sub;

use bezier_rs::Identifier;
use bezier_rs::ManipulatorGroup;
use bezier_rs::Subpath;
use bezier_rs::SubpathTValue;
use bezier_rs::TValueType;
use glam::DVec2;
use godot::classes::rendering_device::DeviceType;
use godot::classes::NoiseTexture2D;
use godot::global::print;
use godot::global::print_verbose;
use godot::global::tan;
use godot::prelude::*;
use godot::classes::Sprite2D;
use godot::classes::ISprite2D;
use bezier_rs::Bezier;
use bezier_rs::TValue;
// use bezier_rs::{ManipulatorGroup, Identifier};
#[derive(Clone, Copy, PartialEq, Eq, Hash, Debug)]
struct NoId;                       // zero-sized local type

impl Identifier for NoId {
    fn new() -> Self { NoId }      // every point gets the same ZST id
}
struct MyExtension;

#[gdextension]
unsafe impl ExtensionLibrary for MyExtension {}


#[derive(GodotClass)]
#[class(base=Sprite2D)]
struct Player {
    speed: f64,
    angular_speed: f64,

    base: Base<Sprite2D>
}

fn trimmed_in_sp(inside: &Subpath<NoId>, outside: &Subpath<NoId>) -> bool {
    outside.point_inside(inside.evaluate(SubpathTValue::GlobalEuclidean(0.5)))
}

fn last(mut sp: Subpath<NoId>) -> DVec2 {
    let lmg: &ManipulatorGroup<NoId> = sp.last_manipulator_group_mut().expect("moet wel");
    let a = lmg.anchor;
    // godot_print!("lmg: {a}");
    a
}

fn first(mut sp: Subpath<NoId>) -> DVec2 {
    // print!("hab");
    let a = sp[0].anchor;
    // godot_print!("first:{a}");
    a
}

fn find_next(sp: &Subpath<NoId>, spvec: &Vec<Subpath<NoId>>) -> usize {
    for i in 0..(spvec.len()) {
        // let sp2 = &spvec[i];
        let differ = last(sp.clone()) - first(spvec[i].clone());
        // godot_print!("{differ}");
        if differ.length() < 0.5 {
           return i; 
        }
    }
    godot_print!("panicking");
    for i in spvec {
        for j in 0..(i.len()-1){
            let anc = i[j].anchor;
            godot_print!("{j}: {anc}")
        }
    }
    panic!()
}
fn flatten_bez(curBez:&Bezier) -> Vec<f64>{
    let mut split_bez1_list: Vec<f64> = Vec::new();
    let h1: DVec2 = Option::expect(curBez.handle_start(), "no handles?");
    let h2: DVec2 = Option::expect(curBez.handle_end(), "no handles?");
    split_bez1_list.push(curBez.start()[0]);split_bez1_list.push(curBez.start()[1]);
    split_bez1_list.push(h1[0]);split_bez1_list.push(h1[1]);
    split_bez1_list.push(h2[0]);split_bez1_list.push(h2[1]);
    split_bez1_list.push(curBez.end()[0]);split_bez1_list.push(curBez.end()[1]);
    // split_bez1_list.push(-1.0);
    return split_bez1_list
}

#[godot_api]
impl ISprite2D for Player {
    fn init(base: Base<Sprite2D>) -> Self {
        godot_print!("Hello, world!"); // Prints to the Godot console
        
        Self {
            speed: 400.0,
            angular_speed: std::f64::consts::PI,
            base,
        }
    }

    fn physics_process(&mut self, delta: f64) {
        // In GDScript, this would be: 
        // rotation += angular_speed * delta
        
        let radians = (self.angular_speed * delta) as f32;
        self.base_mut().rotate(radians);
        // The 'rotate' method requires a f32, 
        // therefore we convert 'self.angular_speed * delta' which is a f64 to a f32
        let rotation = self.base().get_rotation();
        let velocity = Vector2::UP.rotated(rotation) * self.speed as f32;
        self.base_mut().translate(velocity * delta as f32);
        
        // or verbose: 
        // let this = self.base_mut();
        // this.set_position(
        //     this.position() + velocity * delta as f32
        // );
    }
}

#[godot_api]
impl Player {
    #[func]
    fn increase_speed(&mut self, amount: f64) {
        self.speed += amount;
        self.base_mut().emit_signal("speed_increased", &[]);
    }

    #[func]
    fn point_along_cubic_euclidean(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.evaluate(TValue::Euclidean(amount));
        return [a[0], a[1]];
    }

    #[func]
    fn point_along_cubic_parametric(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.evaluate(TValue::Parametric(amount));
        return [a[0], a[1]];
    }


    #[func]
    fn find_intersections(&mut self, a:Array<f64>, b:Array<f64>) -> Vec<f64>  {
        // just return a flat list of the new outline, the merging needs to happen in rust
        // ga door de segmenten van heen van de vorm, bij tval splitten, tot een segment gevonden is dat compleet in boolean ligt
        // 
        let mut return_vec: Vec<f64> = Vec::new();
        let mut bez1_list: Vec<Bezier> = Vec::new();
        let mut bez2_list: Vec<Bezier> = Vec::new();
        for j in 0..(a.len() / 8) {
            let i = j * 8;
            let x1 = a.at(i+0); let y1 = a.at(i+1);
            let x2 = a.at(i+2); let y2 = a.at(i+3);
            let x3 = a.at(i+4); let y3 = a.at(i+5);
            let x4 = a.at(i+6); let y4 = a.at(i+7);
            let bez1 = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
            bez1_list.push(bez1);
        }
        let sp1: Subpath<NoId> = Subpath::<NoId>::from_beziers(&bez1_list[..], true);

        for j in 0..(b.len() / 8) {
            let i = j * 8;
            let x1 = b.at(i+0); let y1 = b.at(i+1);
            let x2 = b.at(i+2); let y2 = b.at(i+3);
            let x3 = b.at(i+4); let y3 = b.at(i+5);
            let x4 = b.at(i+6); let y4 = b.at(i+7);
            let bez1 = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
            bez2_list.push(bez1);
        }
        let sp2: Subpath<NoId> = Subpath::<NoId>::from_beziers(&bez2_list[..], true);
        let sp1_intersections: Vec<(usize, f64)> = sp1.subpath_intersections(&sp2, Some(0.05), Some(0.05));
        let sp2_intersections: Vec<(usize, f64)> = sp2.subpath_intersections(&sp1, Some(0.05), Some(0.05));
        if sp1_intersections.len() == 0 || sp2_intersections.len() == 0 {
            return return_vec
        }
        let mut sp1_intersections_global: Vec<SubpathTValue> = Vec::new();
        for index_and_t in sp1_intersections {
            let ind = index_and_t.0;
            let tval = index_and_t.1;
            let sptvalue = SubpathTValue::Parametric{ segment_index: (ind), t: (tval) };
            sp1_intersections_global.push(sptvalue);
        }
        let mut sp2_intersections_global: Vec<SubpathTValue> = Vec::new();
        for index_and_t in sp2_intersections {
            let ind = index_and_t.0;
            let tval = index_and_t.1;
            let sptvalue = SubpathTValue::Parametric { segment_index: (ind), t: (tval) };
            sp2_intersections_global.push(sptvalue);
        }
        let mut trimmed_sp1: Vec<Subpath<NoId>> = Vec::new();
        for i in 0..sp1_intersections_global.len(){
            let t1 = sp1_intersections_global[i];
            let t2: SubpathTValue;
            if i == sp1_intersections_global.len() - 1 {
                t2 = sp1_intersections_global[0];
            } else {
                t2 = sp1_intersections_global[i+1];
            }
            trimmed_sp1.push(sp1.trim(t1, t2));
        }
        let mut trimmed_sp2: Vec<Subpath<NoId>> = Vec::new();
        for i in 0..sp2_intersections_global.len(){
            let t1 = sp2_intersections_global[i];
            let t2: SubpathTValue;
            if i == sp2_intersections_global.len() - 1 {
                t2 = sp2_intersections_global[0];
            } else {
                t2 = sp2_intersections_global[i+1];
            }
            trimmed_sp2.push(sp2.trim(t1, t2));
        }
        // godot_print!("\nsubs in sp1");
        // for sp in &trimmed_sp1 {
        //     let strt = sp[0].anchor / 128.0;
        //     godot_print!("\nstart: {strt}");
        //     let scnd = sp[1].anchor / 128.0;
        //     godot_print!("\n{scnd}");
        //     let lst = sp[sp.len()-1].anchor / 128.0;
        //     godot_print!("{lst}");
        // }
        // godot_print!("\nsubs in sp2");
        // for sp in &trimmed_sp2 {
        //     let strt = sp[0].anchor / 128.0;
        //     godot_print!("\nstart: {strt}");
        //     let scnd = sp[1].anchor / 128.0;
        //     godot_print!("\n{scnd}");
        //     let lst = sp[sp.len()-1].anchor / 128.0;
        //     godot_print!("{lst}");
        // }

        let mut walkedsubs: Vec<&Subpath<NoId>> = Vec::new();
        let mut allLoops: Vec<Vec<&Subpath<NoId>>> = Vec::new();
        // godot_print!("before");
        let mut c: usize = 0;
        for s in &trimmed_sp1{
            // godot_print!("{c}");
            // for t in s.anchors() {
                // godot_print!("{t}");
            // }
            c+=1;
        }
        // godot_print!("firstpassed");
        for _z in 0..100 {
            let mut currentLoop: Vec<&Subpath<NoId>> = Vec::new();
            let mut startsub: &Subpath<NoId> = &trimmed_sp1[0];
            let mut foundNewSub = false;
            let mut currentlyInMainShape: bool = false;

            for sp in &trimmed_sp1 {
                if !walkedsubs.contains(&sp) && !trimmed_in_sp(sp, &sp2){
                    startsub = sp;
                    walkedsubs.push(&startsub);
                    currentLoop.push(startsub);
                    foundNewSub = true;
                    break;
                }
            }

            if !foundNewSub {
                break;
            }

            // godot_print!("startsub: ");
            // let strt = startsub[0].anchor / 128.0;
            // godot_print!("\nstart: {strt}");
            // let scnd = startsub[1].anchor / 128.0;
            // godot_print!("\n{scnd}");
            // let lst = startsub[startsub.len()-1].anchor / 128.0;
            // godot_print!("{lst}");
            let mut currentsub = &trimmed_sp2[find_next(startsub, &trimmed_sp2)];
            for _i in 0..100 {
                let strt = currentsub[0].anchor / 128.0;
                // godot_print!("\nstart: {strt}");
                // let scnd = currentsub[1].anchor / 128.0;
                // godot_print!("\n{scnd}");
                // let lst = currentsub[currentsub.len()-1].anchor / 128.0;
                // godot_print!("{lst}");
                if currentsub == startsub {
                    break;
                }
                walkedsubs.push(&currentsub);
                currentLoop.push(currentsub);
                if currentlyInMainShape
                {
                    currentsub = &trimmed_sp2[find_next(currentsub, &trimmed_sp2)];
                    currentlyInMainShape = false;
                } else {
                    currentsub = &trimmed_sp1[find_next(currentsub, &trimmed_sp1)];
                    currentlyInMainShape = true;
                }
                // walkedsubs.push(&currentsub);
                // currentLoop.push(currentsub);
            }
            // godot_print!("lengte: ");
            // let clen = currentLoop.len();
            // godot_print!("{clen}");
            // godot_print!("\n\nagagag");
            allLoops.push(currentLoop);
        }
        // start aan het begin van trimmedsub1, check of die sub in de boolean ligt, zoja, neem de volgende sub.
        // vind de pos van het eindpunt van de huidige vsub, vind het matchende beginpunt van een bsub,
        // blijf dit doen tot je een al belopen vsub bereikt.
        // check voor alle vsubs of ze of 1. in de bool liggen, of 2. al belopen zijn. Vind je een onbelopen buiten vsub, start het belopen overnieuw.
        // doe dit totdat er geen onbelopen buiten bsubs meer zijn.
        // converteer de belopen loops naar beziers, exporteer met markers (-9999) ertussen voor elke nieuwe loop
        for lop in allLoops {
            for ss in lop {
                for i in 0..(ss.len() - 1) {
                    // let bb = ss.get_segment(i).expect("ojee");
                    return_vec.append(&mut flatten_bez(&ss.get_segment(i).expect("ojee")));
                }
            }
            return_vec.push(-9999.0);
        }
        godot_print!("\n\noutput:");
        let mut counter = 0;
        // for i in &return_vec {
        //     counter += 1;
        //     let toprint = i / 128.0;
        //     godot_print!("{toprint}")
        // }
        // godot_print!("output length: {counter}");

        return_vec
    }


    #[func]
    fn length_cubic(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> f64  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        return bez.length(Some(1000));
    }

    #[func]
    fn bezier_trimmed(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, t1: f64, t2:f64, param:bool) -> [f64; 8]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let trimmed: Bezier;
        if param{
            trimmed = bez.trim(TValue::Parametric(t1),TValue::Parametric(t2));
        }
        else {
            trimmed = bez.trim(TValue::Euclidean(t1),TValue::Euclidean(t2));
        }
        let h1: DVec2 = Option::expect(trimmed.handle_start(), "no handles?");
        let h2: DVec2 = Option::expect(trimmed.handle_end(), "no handles?");
        return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y];
        // return bez.length(Some(1000));
    }

    #[func]
    fn trimmed_tangent_parametric(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, t1: f64, t2: f64) -> [f64; 12]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let trimmed: Bezier;
        trimmed = bez.trim(TValue::Euclidean(t1),TValue::Euclidean(t2));
        // trimmed = bez;

        let tan_start = trimmed.tangent(TValue::Euclidean(0.01));
        let tan_end = trimmed.tangent(TValue::Euclidean(0.99));
        let h1: DVec2 = Option::expect(trimmed.handle_start(), "no handles?");
        let h2: DVec2 = Option::expect(trimmed.handle_end(), "no handles?");
        return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y, tan_start[0], tan_start[1], tan_end[0], tan_end[1]];
        // return [trimmed.start().x, trimmed.start().y]
        // return [a[0], a[1], b[0], b[1]];
    }


    #[func]
    fn tangent_parametric(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.tangent(TValue::Parametric(amount));
        return [a[0], a[1]];
    }

    #[signal]
    fn speed_increased();
}
