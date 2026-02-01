mod path_boolean;
// #[cfg(feature = "parsing")]
mod util {
	pub(crate) mod aabb;
	pub(crate) mod epsilons;
	pub(crate) mod math;
	pub(crate) mod grid;
}
mod path;

mod parsing {
	pub(crate) mod path_command;
	pub(crate) mod path_data;
}
use kurbo::Point;
pub(crate) use parsing::*;
pub(crate) use path::*;
pub(crate) use util::*;
use kurbo;

pub use intersection_path_segment::path_segment_intersection;
#[cfg(feature = "parsing")]
pub use parsing::path_data::{path_from_path_data, path_to_path_data};
pub use path_boolean::{BooleanError, EPS, FillRule, PathBooleanOperation, path_boolean};
pub use path_segment::PathSegment;

pub(crate) mod compare;

mod bezier;
mod consts;
mod poisson_disk;
mod polynomial;
mod subpath;
mod symmetrical_basis;
mod utils;

pub use bezier::*;
pub use subpath::*;
pub use symmetrical_basis::*;
pub use utils::{Cap, Join, SubpathTValue, TValue, TValueType};
use std::ops::Range;
use std::ops::Sub;

use glam::DVec2;
use godot::classes::rendering_device::DeviceType;
use godot::classes::NoiseTexture2D;
use godot::global::print;
use godot::global::print_verbose;
use godot::global::tan;
use godot::prelude::*;
use godot::classes::Sprite2D;
use godot::classes::ISprite2D;
use std::fmt::Debug;
use spline;

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

fn array_to_subpath(a:Array<f64>) -> Subpath<NoId> {
    let mut beza_list: Vec<Bezier> = Vec::new();
    for j in 0..(a.len() / 8) {
        let i = j * 8;
        let x1 = a.at(i+0); let y1 = a.at(i+1);
        let x2 = a.at(i+2); let y2 = a.at(i+3);
        let x3 = a.at(i+4); let y3 = a.at(i+5);
        let x4 = a.at(i+6); let y4 = a.at(i+7);
        beza_list.push(Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4));
    }
    return Subpath::<NoId>::from_beziers(&beza_list[..], true);
}

fn array_to_path(a:Array<f64>) -> Path  {
    let mut out_path: Path = Path::new();
    for j in 0..(a.len() / 8) {
        let i = j * 8;
        let x1 = a.at(i+0); let y1 = a.at(i+1);
        let x2 = a.at(i+2); let y2 = a.at(i+3);
        let x3 = a.at(i+4); let y3 = a.at(i+5);
        let x4 = a.at(i+6); let y4 = a.at(i+7);
        let new_segment = PathSegment::Cubic(
            DVec2::new(x1,y1).ceil(),
            DVec2::new(x2,y2).ceil(),
            DVec2::new(x3,y3).ceil(),
            DVec2::new(x4,y4).ceil()
        );
        out_path.push(new_segment);
    }
    return out_path
}

fn path_to_subpath(p: &Path) -> Subpath<NoId> {
    let mut bezier_list: Vec<Bezier> = Vec::new();
    for segment in p {
        let bb = segment.to_cubic();
        let bez = Bezier::from_cubic_dvec2(bb[0], bb[1], bb[2], bb[3]);
        bezier_list.push(bez);
    }
    return Subpath::<NoId>::from_beziers(&bezier_list, true)
}

fn path_to_vec_float(p: &Path) -> Vec<f64> {
    let mut return_vec: Vec<f64> = Vec::new();
    for segment in p {
        let bb = segment.to_cubic();
        return_vec.push(bb[0].x); return_vec.push(bb[0].y);
        return_vec.push(bb[1].x); return_vec.push(bb[1].y);
        return_vec.push(bb[2].x); return_vec.push(bb[2].y);
        return_vec.push(bb[3].x); return_vec.push(bb[3].y);
    }
    return return_vec
}

fn last(mut sp: Subpath<NoId>) -> DVec2 {
    let lmg: &ManipulatorGroup<NoId> = sp.last_manipulator_group_mut().expect("moet wel");
    let a = lmg.anchor;
    // godot_print!("lmg: {a}");
    a
}

fn first(sp: Subpath<NoId>) -> DVec2 {
    // print!("hab");
    let a = sp[0].anchor;
    // godot_print!("first:{a}");
    a
}

fn find_previous(sp: &Subpath<NoId>, spvec: &Vec<Subpath<NoId>>) -> usize {
    // godot_print!("find previous");
    for i in 0..(spvec.len()) {
        // let sp2 = &spvec[i];
        // let fs = first(sp.clone());
        // let ls = last(spvec[i].clone());
        // godot_print!("{i} {fs}, {ls}");
        
        let differ = last(sp.clone()) - last(spvec[i].clone());
        // let l1 = last(sp.clone());
        // let l2 = last(spvec[i].clone());
        // if (l1[0] - l2[0]).abs() < 1.0 && (l1[1] - l2[1]).abs() < 1.0 {
        //    return i; 
        // }
        // godot_print!("{differ}");
        if differ.length() < 0.3 {
        //    godot_print!("{i}");
           return i; 
        }
    }
    godot_print!("panicking");
    for i in spvec {
        for j in 0..(i.len()-1){
            let anc = i[j].anchor;
            let ancc = anc / 128.0;
            godot_print!("{j}: {anc} {ancc}")
        }
    }
    let anc1 = sp[0].anchor;
    let anc2 = sp[1].anchor;
    let anc3 = sp[sp.len()-1].anchor;
    godot_print!("{anc1} {anc2} {anc3}");
    panic!("cannot find previous")
}

fn find_next(sp: &Subpath<NoId>, spvec: &Vec<Subpath<NoId>>) -> usize {
    for i in 0..(spvec.len()) {
        // let sp2 = &spvec[i];
        // let l1 = last(sp.clone());
        // let l2 = first(spvec[i].clone());
        // if (l1[0] - l2[0]).abs() < 1.0 && (l1[1] - l2[1]).abs() < 1.0 {
        //    return i; 
        // }
        let differ = last(sp.clone()) - first(spvec[i].clone());
        // godot_print!("{differ}");
        if differ.length() < 0.3 {
           return i; 
        }
    }
    godot_print!("panicking");
    for i in spvec {
        for j in 0..(i.len()-1){
            let anc = i[j].anchor;
            let ancc = anc / 128.0;
            godot_print!("{j}: {anc} {ancc}")
        }
    }
    let anc1 = sp[0].anchor;
    let anc2 = sp[1].anchor;
    let anc3 = sp[sp.len()-1].anchor;
    godot_print!("{anc1} {anc2} {anc3}");
    panic!("cannot find next")
}
fn flatten_bez(cb:&Bezier, rever: bool ) -> Vec<f64>{
    let mut split_bez1_list: Vec<f64> = Vec::new();
    let curBez = &(cb.to_cubic());
    let h1: DVec2 = Option::expect(curBez.handle_start(), "no handles?");
    let h2: DVec2 = Option::expect(curBez.handle_end(), "no handles?");
    if !rever {
        split_bez1_list.push(curBez.start()[0]);split_bez1_list.push(curBez.start()[1]);
        split_bez1_list.push(h1[0]);split_bez1_list.push(h1[1]);
        split_bez1_list.push(h2[0]);split_bez1_list.push(h2[1]);
        split_bez1_list.push(curBez.end()[0]);split_bez1_list.push(curBez.end()[1]);
    }
    else 
    {
        split_bez1_list.push(curBez.end()[0]);split_bez1_list.push(curBez.end()[1]);
        split_bez1_list.push(h2[0]);split_bez1_list.push(h2[1]);
        split_bez1_list.push(h1[0]);split_bez1_list.push(h1[1]);
        split_bez1_list.push(curBez.start()[0]);split_bez1_list.push(curBez.start()[1]);

    }
    // split_bez1_list.push(-1.0);
    return split_bez1_list
}

fn is_subpath_in_list(sp: &Subpath<NoId>, lst:&Vec<Subpath<NoId>>) -> bool {
    'outer: for sp2 in lst {
        for i in 0..sp.len(){
            if (sp[i].anchor - sp2[i].anchor).length() > 1.0 {
                continue 'outer
            }
        }
        return true
    }
    sp.reverse();
    'outer: for sp2 in lst {
        for i in 0..sp.len(){
            if (sp[i].anchor - sp2[i].anchor).length() > 1.0 {
                continue 'outer
            }
        }
        return true
    }
    return false
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
    fn point_along_cubic_parametric(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, amount: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.evaluate(TValue::Parametric(amount));
        return [a[0], a[1]];
    }



    #[func]
    fn vector_boolean(&mut self, a:Array<f64>, b:Array<f64>, negative: bool) -> Vec<f64>  {

        // just return a flat list of the new outline, the merging needs to happen in rust
        // ga door de segmenten van heen van de vorm, bij tval splitten, tot een segment gevonden is dat compleet in boolean ligt
        // 
        let mut return_vec: Vec<f64> = Vec::new();
        let mut bez1_list: Vec<Bezier> = Vec::new();
        let mut bez2_list: Vec<Bezier> = Vec::new();
        
        godot_print!("\n\nPrinting the in put vector:{a}\n");
        for j in 0..(a.len() / 8) {
            let i = j * 8;
            let x1 = a.at(i+0); let y1 = a.at(i+1);
            let x2 = a.at(i+2); let y2 = a.at(i+3);
            let x3 = a.at(i+4); let y3 = a.at(i+5);
            let x4 = a.at(i+6); let y4 = a.at(i+7);
            let mut bez1 = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
            // bez1 = bez1.to_linear();
            bez1_list.push(bez1);
        }
        let sp1: Subpath<NoId> = Subpath::<NoId>::from_beziers(&bez1_list[..], true);
        // if sp1.self_intersections(Some(0.05),Some( 0.05)).len() > 0{
        //     return return_vec
        // }

        for j in 0..(b.len() / 8) {
            let i = j * 8;
            let x1 = b.at(i+0); let y1 = b.at(i+1);
            let x2 = b.at(i+2); let y2 = b.at(i+3);
            let x3 = b.at(i+4); let y3 = b.at(i+5);
            let x4 = b.at(i+6); let y4 = b.at(i+7);
            let mut bez1 = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
            // bez1 = bez1.to_linear();
            bez2_list.push(bez1);
        }
        let sp2: Subpath<NoId> = Subpath::<NoId>::from_beziers(&bez2_list[..], true);
        // if sp2.self_intersections(Some(0.05),Some( 0.05)).len() > 0{
        //     return return_vec
        // }
        let sp2len = sp2.len();
        godot_print!("{sp2len}");
        let sp1_intersections_unfiltered: Vec<(usize, f64)> = sp1.subpath_intersections(&sp2, Some(0.15), Some(0.05));
        let sp2_intersections_unfiltered: Vec<(usize, f64)> = sp2.subpath_intersections(&sp1, Some(0.15), Some(0.05));
        let mut sp1_intersections: Vec<(usize,f64)> = Vec::new();
        let mut sp2_intersections: Vec<(usize,f64)> = Vec::new();
        for i in sp1_intersections_unfiltered{
            let mut same = false;
            for j in &sp1_intersections {
                if i.0 == j.0 && (i.1 - j.1).abs() < 0.01 {
                    same = true;
                    break
                }
            }
            if !same {
                sp1_intersections.push(i)
            }
        }
        for i in sp2_intersections_unfiltered{
            let mut same = false;
            for j in &sp2_intersections {
                if i.0 == j.0 && (i.1 - j.1).abs() < 0.01 {
                    same = true;
                    break
                }
            }
            if !same {
                sp2_intersections.push(i)
            }
        }
        let sp1ilen = sp1_intersections.len();
        let sp2ilen = sp2_intersections.len();
        godot_print!("intersectionlengths: {sp1ilen} {sp2ilen}");
        if sp1_intersections.len() == 0 || sp2_intersections.len() == 0 {
            return return_vec
        }
        for ss in &sp1_intersections {
            let aa = ss.0;
            let bb = ss.1;
            godot_print!("sp1: {aa} {bb}");
        }
        for ss in &sp2_intersections {
            let aa = ss.0;
            let bb = ss.1;
            godot_print!("sp1: {aa} {bb}");
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
        godot_print!("anchorssssssss!");
        for i in 0..sp1_intersections_global.len(){
            let t1 = sp1_intersections_global[i];
            let t2: SubpathTValue;
            if i == sp1_intersections_global.len() - 1 {
                t2 = sp1_intersections_global[0];
            } else {
                t2 = sp1_intersections_global[i+1];
            }
            let mut trmmd = sp1.trim(t1,t2);
            if trmmd.length(Some(3.0)) >= 1.0 && !is_subpath_in_list(&trmmd, &trimmed_sp1){
                for mg in trmmd.manipulator_groups_mut(){
                    let anch = mg.anchor;
                    godot_print!("{anch}");
                    mg.anchor = mg.anchor.round();
                    let anch = mg.anchor;
                    godot_print!("{anch}");
                }
                trimmed_sp1.push(trmmd);
            }
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
            let mut trmmd = sp2.trim(t1,t2);
            if trmmd.length(Some(3.0)) >= 1.0 && !is_subpath_in_list(&trmmd, &trimmed_sp2){
                for mg in trmmd.manipulator_groups_mut(){
                    mg.anchor = mg.anchor.round();
                }
                trimmed_sp2.push(trmmd);
            }
        }
        if trimmed_sp1.len() == 0 || trimmed_sp2.len() == 0 {
            return return_vec
        }
        godot_print!("\nsubs in sp1");
        for sp in &trimmed_sp1 {
            let strt = sp[0].anchor;
            godot_print!("\nstart: {strt}");
            let scnd = sp[1].anchor;
            godot_print!("\n{scnd}");
            let lst = sp[sp.len()-1].anchor;
            godot_print!("{lst}");
        }
        godot_print!("\nsubs in sp2");
        for sp in &trimmed_sp2 {
            let strt = sp[0].anchor / 128.0;
            godot_print!("\nstart: {strt}");
            let scnd = sp[1].anchor / 128.0;
            godot_print!("\n{scnd}");
            let lst = sp[sp.len()-1].anchor / 128.0;
            godot_print!("{lst}");
        }

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
            let mut foundNewSub = false;
            let mut currentlyInMainShape: bool = false;
            if trimmed_sp1.len() == 0{
                panic!("sp1 is lengte 0")
            }
            if trimmed_sp2.len() == 0{
                let tr1 = &trimmed_sp1;
                godot_print!("panicking");
                for ttt in tr1 {
                    let mut aaa = ttt[0].anchor;
                    godot_print!("start: {aaa}");
                    aaa = ttt[1].anchor;
                    godot_print!("{aaa}");
                    aaa = ttt[ttt.len()-1].anchor;
                    godot_print!("final: {aaa}");
                }
                panic!("sp2 is lengte 0")
            }
            let mut startsub: &Subpath<NoId> = &trimmed_sp1[0];
            let mut outside = &sp2;
            let mut inside = &trimmed_sp1;
            if negative {
                startsub = &trimmed_sp2[0];
                outside = &sp1;
                inside = &trimmed_sp2;
            }

            for sp in inside {
                if !walkedsubs.contains(&sp) && (negative == trimmed_in_sp(sp, outside)){
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

            godot_print!("startsub: ");
            // let strt = startsub[0].anchor / 128.0;
            // godot_print!("\nstart: {strt}");
            let scnd = startsub[1].anchor / 128.0;
            godot_print!("\n{scnd}");
            let lst = startsub[startsub.len()-1].anchor / 128.0;
            godot_print!("{lst}");
            let mut currentsub = &trimmed_sp2[find_next(startsub, &trimmed_sp2)];
            if negative {
                currentsub = &trimmed_sp1[find_previous(startsub, &trimmed_sp1)];
            }
            for _i in 0..100 {
                // let strt = currentsub[0].anchor / 128.0;
                // godot_print!("\nstart: {strt}");
                let scnd = currentsub[1].anchor / 128.0;
                godot_print!("\n{scnd}");
                let lst = currentsub[currentsub.len()-1].anchor / 128.0;
                godot_print!("{lst}");
                if currentsub == startsub {
                    break;
                }
                walkedsubs.push(&currentsub);
                currentLoop.push(currentsub);
                if negative {
                    if !currentlyInMainShape
                    {
                        currentsub = &trimmed_sp2[find_previous(currentsub, &trimmed_sp2)];
                        currentlyInMainShape = true;
                    } else {
                        currentsub = &trimmed_sp1[find_previous(currentsub, &trimmed_sp1)];
                        currentlyInMainShape = false;
                    }
                }
                else {
                    if currentlyInMainShape
                    {
                        currentsub = &trimmed_sp2[find_next(currentsub, &trimmed_sp2)];
                        currentlyInMainShape = false;
                    } else {
                        currentsub = &trimmed_sp1[find_next(currentsub, &trimmed_sp1)];
                        currentlyInMainShape = true;

                    }
                }
                // walkedsubs.push(&currentsub);
                // currentLoop.push(currentsub);
                godot_print!("{_i}");
            }
            godot_print!("lengte: ");
            let clen = currentLoop.len();
            godot_print!("{clen}");
            godot_print!("\n\nagagag");
            // godot_print!("{currentLoop}");
            for sp in &currentLoop {
                godot_print!("new subpath");
                for i in 0..sp.len() {
                    let anc = sp[i].anchor / 128.0;
                    godot_print!("{anc}");
                }
            }
            allLoops.push(currentLoop);
        }
        // start aan het begin van trimmedsub1, check of die sub in de boolean ligt, zoja, neem de volgende sub.
        // vind de pos van het eindpunt van de huidige vsub, vind het matchende beginpunt van een bsub,
        // blijf dit doen tot je een al belopen vsub bereikt.
        // check voor alle vsubs of ze of 1. in de bool liggen, of 2. al belopen zijn. Vind je een onbelopen buiten vsub, start het belopen overnieuw.
        // doe dit totdat er geen onbelopen buiten bsubs meer zijn.
        // converteer de belopen loops naar beziers, exporteer met markers (-9999) ertussen voor elke nieuwe loop
        for mut lop in allLoops {
            let mut counterrr = 0;
            if negative {
                lop.reverse();
            }
            for ss in lop {
                for i in 0..(ss.len() - 1) {
                    // let bb = ss.get_segment(i).expect("ojee");
                    if negative && counterrr%2 != 0{
                        return_vec.append(&mut flatten_bez(&ss.reverse().get_segment(i).expect("ojee"), false));
                    }
                    else {
                        return_vec.append(&mut flatten_bez(&ss.get_segment(i).expect("ojee"), false));
                    }
                }
                counterrr += 1;
            }
            return_vec.push(-9999.0);
        }
        godot_print!("\n\noutput:");
        let mut counter = 0;
        // for i in (&return_vec) {
        //     let val = i / 128.0;
        //     godot_print!("{val}");
        // }
        for i in 0..(&return_vec.len() / 8) {
            counter += 1;
            let toprint = return_vec[i*8 ] / 128.0;
            let toprint2 = return_vec[i*8 +1] / 128.0;
            let toprint3 = return_vec[i*8 +6] / 128.0;
            let toprint4 = return_vec[i*8 +7] / 128.0;
            godot_print!("{toprint} {toprint2} {toprint3} {toprint4}")
        }
        godot_print!("output length: {counter}");

        return_vec
    }


    #[func]
    fn length_cubic(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> f64  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        return bez.length(Some(1000.0));
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

        let h1: DVec2 = Option::expect(trimmed.handle_start(), "no handles?");
        let h2: DVec2 = Option::expect(trimmed.handle_end(), "no handles?");

        if trimmed.length(Some(0.01)) < 1.0 {
            return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y, 0.,0.,0.,0.];
        }

        let tan_start = trimmed.tangent(TValue::Euclidean(0.01));
        let tan_end = trimmed.tangent(TValue::Euclidean(0.99));
        return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y, tan_start[0], tan_start[1], tan_end[0], tan_end[1]];
    }


    #[func]
    fn tangent_parametric(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, amount: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.tangent(TValue::Parametric(amount));
        return [a[0], a[1]];
    }

    #[signal]
    fn speed_increased();

    fn floats_to_paths(a:Array<f64>) -> Path {
        let mut a_path: Path = Path::new();
        a_path
    }


    #[func]
    fn are_shapes_overlapping(&mut self, ai:Array<i16>, bi:Array<i16>) -> bool {
        let mut a: Array<f64> = Array::new();
        let mut b: Array<f64> = Array::new();
        for i in 0..ai.len(){
            a.push(ai.at(i) as f64);
        }
        for i in 0..bi.len(){
            b.push(bi.at(i) as f64);
        }
        let bezier_path_a = array_to_subpath(a.clone());
        let bezier_path_b = array_to_subpath(b.clone());
        return bezier_path_a.subpath_intersections(&bezier_path_b, Some(0.1), Some(0.1)).len() > 0;
    }


    #[func]
    fn shape_shape_intersections(&mut self, shape:Array<f64>, shape2:Array<f64>) -> Vec<f64>  {
        let mut return_vec: Vec<f64> = Vec::new();
        let bezier_path = array_to_subpath(shape.clone());
        let bezier_path2 = array_to_subpath(shape2.clone());
        // let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);

        let inters: Vec<(usize, f64)> = bezier_path2.subpath_intersections(&bezier_path,Some(0.1),Some(0.1));
        for i in 0..bezier_path.len_segments()
        {
            let aj = bezier_path.get_segment(i).unwrap();
            let ps = aj.get_points();
            let inter = bezier_path2.intersections(&bezier_path.get_segment(i).unwrap(), Some(0.1), Some(0.1));

        }

        for inter in inters{
            let intersection_coords: DVec2 = bezier_path2.get_segment(inter.0).unwrap().evaluate(TValue::Parametric(inter.1));
            return_vec.push(inter.0 as f64);
            return_vec.push(intersection_coords.x);
            return_vec.push(intersection_coords.y);
        }
        return return_vec;
    }

    #[func]
    fn segment_shape_intersections(&mut self, shape:Array<f64>, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> Vec<Vector2>  {
        let mut return_vec: Vec<Vector2> = Vec::new();
        let bezier_path = array_to_subpath(shape.clone());
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);

        let inters: Vec<(usize, f64)> = bezier_path.intersections(&bez,Some(5.1),Some(5.0));
        for inter in inters{
            let intersection_coords: DVec2 = bezier_path.get_segment(inter.0).unwrap().evaluate(TValue::Parametric(inter.1));
            let v: Vector2 = Vector2::new(intersection_coords.x as f32, intersection_coords.y as f32);
            return_vec.push(v);
            // return_vec.push(intersection_coords.x);
            // return_vec.push(intersection_coords.y);
        }
        return return_vec;
    }

    #[func]
    fn project_on_shape_tangent(&mut self, shape:Array<f64>, x:f64, y:f64) -> Vector2  {
        let bezier_path = array_to_subpath(shape.clone());
        let inter = bezier_path.project(DVec2::new(x,y));
        let inter = inter.unwrap();
        let seg = bezier_path.get_segment(inter.0).unwrap();
        let tan = seg.tangent(TValue::Parametric(inter.1));
        let return_vec = Vector2::new(tan.x as f32, tan.y as f32); 
        return return_vec;
    }

    #[func]
    fn project_on_shape(&mut self, shape:Array<f64>, x:f64, y:f64) -> Vector2  {
        let bezier_path = array_to_subpath(shape.clone());
        let inter = bezier_path.project(DVec2::new(x,y));
        let inter = inter.unwrap();
        let intersection_coords: DVec2 = bezier_path.get_segment(inter.0).unwrap().evaluate(TValue::Parametric(inter.1));
        let return_vec = Vector2::new(intersection_coords.x as f32, intersection_coords.y as f32); 
        return return_vec;
    }

    #[func]
    fn better_vector_boolean(&mut self, ai:Array<i16>, bi:Array<i16>, negative: bool) -> Vec<f64>  {
        godot_print!("\n\n {negative}");
        let mut return_vec: Vec<f64> = Vec::new();
        let mut a: Array<f64> = Array::new();
        let mut b: Array<f64> = Array::new();
        let diver: f64 = 128.0;
        for i in 0..ai.len(){
            a.push(ai.at(i) as f64);
        }
        for i in 0..bi.len(){
            b.push(bi.at(i) as f64);
        }
        let alen = a.len();
        let boolean_path_a = array_to_path(a.clone());
        let boolean_path_b = array_to_path(b.clone());
        let bezier_path_a = array_to_subpath(a.clone());
        let bezier_path_b = array_to_subpath(b.clone());
        for j in 0..(a.len()/8) {
            let i = j * 8;
            let val = a.at(i) / diver;
            let val1 = a.at(i+1) / diver;
            let val2 = a.at(i+2) / diver;
            let val3 = a.at(i+3) / diver;
            let val4 = a.at(i+4) / diver;
            let val5 = a.at(i+5) / diver;
            let val6 = a.at(i+6) / diver;
            let val7 = a.at(i+7) / diver;
            godot_print!("RUST a input {i} : {val} {val1} . {val2} {val3} . {val4} {val5} . {val6} {val7}")
        }
        for j in 0..(b.len()/8) {
            let i = j * 8;
            let val = b.at(i) / diver;
            let val1 = b.at(i+1) / diver;
            let val2 = b.at(i+2) / diver;
            let val3 = b.at(i+3) / diver;
            let val4 = b.at(i+4) / diver;
            let val5 = b.at(i+5) / diver;
            let val6 = b.at(i+6) / diver;
            let val7 = b.at(i+7) / diver;
            godot_print!("RUST b input {i} : {val} {val1} . {val2} {val3} . {val4} {val5} . {val6} {val7}")
        }

        for i in bezier_path_a.manipulator_groups(){
            let a = i.anchor / diver;
            let ih = i.in_handle.unwrap() / diver;
            let oh = i.out_handle.unwrap() / diver;
            godot_print!("RUST a man group: {a} {ih} {oh}")
        }
        for i in bezier_path_b.manipulator_groups(){
            let a = i.anchor / diver;
            let ih = i.in_handle.unwrap() / diver;
            let oh = i.out_handle.unwrap() / diver;
            godot_print!("RUST b man group: {a} {ih} {oh}")
        }
        godot_print!("RUST: a input length: {alen}");
        let anlen = bezier_path_a.anchors().len();
        godot_print!("RUST: a bezier anchors  length: {anlen}");

        // check of 1 van de vormen self-intersects of colinear is (alle punten van de vorm liggen op 1 lijn)
        if bezier_path_a.area(Some(0.1), Some(0.1)) < 0.99 
        {
            godot_print!("RUST: path a tiny area");
            godot_error!("ar");
            return return_vec 
        }
        if bezier_path_b.area(Some(0.1), Some(0.1)) < 0.99 
        {
            godot_print!("RUST: path b tiny area");
            godot_error!("ar");
            return return_vec 
        }
        if bezier_path_a.all_self_intersections(Some(3.1), Some(3.1)).len() > 0 
        {
            godot_print!("RUST: path a self intersects");
            godot_error!("ar");
            // return return_vec 
        }

        if bezier_path_b.all_self_intersections(Some(3.1), Some(3.1)).len() > 0 
        { 
            let ts= bezier_path_b.all_self_intersections(Some(3.1), Some(3.1));
            let tslen = ts.len();
            let ts00 = ts[0].0;
            let ts01 = ts[0].1;
            let ts10 = ts[1].0;
            let ts11 = ts[1].1;
            godot_print!("RUST: path b self intersects {tslen} {ts00} {ts01} {ts10} {ts11}");
            godot_error!("ar");
            // return return_vec 
        }

        // hier beginnen we
        let bool_result = path_boolean(
            &boolean_path_a,
            FillRule::NonZero,
            &boolean_path_b,
            FillRule::NonZero,
            if !negative {PathBooleanOperation::Union} else {PathBooleanOperation::Difference}
        );
        // let errr = bool_result;
        let result_path_list = bool_result.unwrap();
        for p in &result_path_list
        {
            for pp in p
            {
                let ppp = pp.to_cubic();
                let ppp0 = ppp[0];
                let ppp1 = ppp[1];
                let ppp2 = ppp[2];
                let ppp3 = ppp[3];
                godot_print!("  RUST pathbool output:::: {ppp0} {ppp1} {ppp2} {ppp3}");
            }
        }

        if result_path_list.len() >= 1
        {
            let lennn = result_path_list.len();
            godot_print!("      RUST: SO many pahts: {lennn}");
        }
        let result_boolean_path_0: &Path = &result_path_list[0];
        // return_vec = [return_vec, path_to_vec_float(result_boolean_path_0)].concat();

        let c = &return_vec;
        // for j in 0..(c.len()/8) {
        //     let i = j * 8;
        //     let val = c.get(i).unwrap() / diver;
        //     let val1 = c.get(i+1).unwrap() / diver;
        //     let val2 = c.get(i+2).unwrap() / diver;
        //     let val3 = c.get(i+3).unwrap() / diver;
        //     let val4 = c.get(i+4).unwrap() / diver;
        //     let val5 = c.get(i+5).unwrap() / diver;
        //     let val6 = c.get(i+6).unwrap() / diver;
        //     let val7 = c.get(i+7).unwrap() / diver;
        //     godot_print!("RUST output {i} : {val} {val1} . {val2} {val3} . {val4} {val5} . {val6} {val7}")
        // }

        // return return_vec;
        let result_bezier_path_0 = path_to_subpath(result_boolean_path_0);
        let sis = result_bezier_path_0.all_self_intersections(Some(0.01), Some(0.01));

        let mut result_0_start: DVec2 = result_boolean_path_0[0].start();
        let mut big_gap: bool = false;
        for ps in result_boolean_path_0 {
            if (ps.start() - ps.end()).length() < 0.05 {
                continue
            }
            if result_0_start.distance(ps.start())>0.99{
                big_gap = true;
                break;
            }
            result_0_start = ps.end();
        }
        // if !big_gap && sis.len() > 0 {
        if false {
            let fsp = result_bezier_path_0;
            let sispos = fsp.evaluate(SubpathTValue::Euclidean{segment_index: sis[0].0, t: sis[0].1});

            let mut old_res: DVec2 = result_boolean_path_0[0].start();

            for ps in result_boolean_path_0 {
                if (ps.start() - ps.end()).length() < 0.05 {
                    continue
                }
                if old_res.distance(ps.start())>0.99 || (ps.start().distance(sispos) < 0.99 && return_vec.len() > 0){
                    return_vec.push(-9999.0);
                }
                old_res = ps.end();
                let temp_bez = ps.to_cubic();
                for p in temp_bez {
                    return_vec.push(p[0]/1.0);
                    return_vec.push(p[1]/1.0);
                }
            }
        }
        else {
            let mut old_res: DVec2 = result_boolean_path_0[0].start();

            for ps in result_boolean_path_0 {
                if (ps.start() - ps.end()).length() < 0.05 {
                    continue
                }
                if old_res.distance(ps.start())>0.9 {
                    return_vec.push(-9999.0);
                }
                for node_or_handle in ps.to_cubic() {
                    return_vec.push(node_or_handle[0]);
                    return_vec.push(node_or_handle[1]);
                }
                old_res = ps.end();
            }
        }
        let c = &return_vec;
        for j in 0..(c.len()/8) {
            let i = j * 8;
            let val = c.get(i).unwrap() / diver;
            let val1 = c.get(i+1).unwrap() / diver;
            let val2 = c.get(i+2).unwrap() / diver;
            let val3 = c.get(i+3).unwrap() / diver;
            let val4 = c.get(i+4).unwrap() / diver;
            let val5 = c.get(i+5).unwrap() / diver;
            let val6 = c.get(i+6).unwrap() / diver;
            let val7 = c.get(i+7).unwrap() / diver;
            godot_print!("RUST output {i} : {val} {val1} . {val2} {val3} . {val4} {val5} . {val6} {val7}")
        }

        return_vec
    }

    #[func]
    fn hyper_bezier_handles(&mut self, start: Vector2, in_points:Array<f64>) -> Vec<f64> {
        // inpoints is a sequence of points defining 
        // on-curve point in (x,y), 
        // and 
        // handle in  (x,y) or (-9999,-9999)
        // handle out (x,y) or (-9999,-9999)
        // on-curve point out (x,y)
        // either 1 or 0 to indicate corner on curve points. 

        // ret_beziers is a list of bezier in-point, in-handle, out-handle, out-point +
        // how many beziers are created for each hyperbezier
        let mut result = Vec::new();
        if in_points.len() < 2 {
            return result;
        }

        // 1. Build the spec: one MoveTo, then SplineTo with auto handles.
        let mut spec = spline::SplineSpec::new();
        let start: kurbo::Point = kurbo::Point::new(start.x as f64, start.y as f64);
        // godot_print!("rust: start: {start}");
        spec.move_to(start);
        for i in 0..(in_points.len() / 7) {
            let p1: Point = Point::new(in_points.at(i*7 + 0), in_points.at(i*7 + 1));
            let p2: Point = Point::new(in_points.at(i*7 + 2), in_points.at(i*7 + 3));
            let p3: Point = Point::new(in_points.at(i*7 + 4), in_points.at(i*7 + 5));
            // godot_print!("rust: {p3}");
            let mut is_smooth: bool = true;
            if in_points.at(i*7 + 6) as i8 == 1 {
               is_smooth = false; 
            }
            spec.spline_to(None, None, p3, is_smooth);
        } 
        spec.close();

        // 2. Solve the spline. This:
        //    - Creates HyperBezier segments
        //    - Solves for node angles and tensions
        let spline = spec.solve();

        // 3. Flatten to cubic Bézier segments: [p0, p1, p2, p3].
        for seg in spline.segments() {
            let p00 = seg.p0;
            let p01 = seg.p1;
            let p02 = seg.p2;
            let p03 = seg.p3;
            result.extend([p00.x, p00.y, p01.x,p01.y, p02.x,p02.y,p03.x,p03.y]);
        }
    return result;
    }

    #[func]
    fn hyper_bezier(&mut self, start: Vector2, in_points:Array<f64>) -> Vec<f64> {
        // inpoints is a sequence of points defining 
        // on-curve point in (x,y), 
        // and 
        // handle in  (x,y) or (-9999,-9999)
        // handle out (x,y) or (-9999,-9999)
        // on-curve point out (x,y)
        // either 1 or 0 to indicate corner on curve points. 

        // ret_beziers is a list of bezier in-point, in-handle, out-handle, out-point +
        // how many beziers are created for each hyperbezier
        let mut result = Vec::new();
        if in_points.len() < 2 {
            return result;
        }

        // 1. Build the spec: one MoveTo, then SplineTo with auto handles.
        let mut spec = spline::SplineSpec::new();
        let start: kurbo::Point = kurbo::Point::new(start.x as f64, start.y as f64);
        // godot_print!("rust: start: {start}");
        spec.move_to(start);
        for i in 0..(in_points.len() / 7) {
            let p1: Point = Point::new(in_points.at(i*7 + 0), in_points.at(i*7 + 1));
            let p2: Point = Point::new(in_points.at(i*7 + 2), in_points.at(i*7 + 3));
            let p3: Point = Point::new(in_points.at(i*7 + 4), in_points.at(i*7 + 5));
            // godot_print!("rust: {p3}");
            let mut is_smooth: bool = true;
            if in_points.at(i*7 + 6) as i8 == 1 {
               is_smooth = false; 
            }
            spec.spline_to(None, None, p3, is_smooth);
            // spec.spline_to(Some(p1), Some(p2), p3, is_smooth);
        } 
        spec.close();

        // 2. Solve the spline. This:
        //    - Creates HyperBezier segments
        //    - Solves for node angles and tensions
        let spline = spec.solve();

        // 3. Flatten to cubic Bézier segments: [p0, p1, p2, p3].
        for seg in spline.segments() {
            let p00 = seg.p0;
            let p01 = seg.p1;
            let p02 = seg.p2;
            let p03 = seg.p3;
            // godot_print!("rust: {p00} {p01} {p02} {p03}");
            let mut p0 = seg.p0;
            let mut element_counter: f64 = 0.0;
            for el in seg.render_elements() {
                match el {
                    kurbo::PathEl::CurveTo(p1, p2, p3) => {
                        result.extend([p0.x, p0.y, p1.x,p1.y, p2.x,p2.y,p3.x,p3.y]);
                        p0 = p3;
                    }
                    kurbo::PathEl::LineTo(p) => {
                        // Optionally turn lines into degenerate cubics:
                        result.extend([p0.x, p0.y, p0.x,p0.y, p.x,p.y,p.x,p.y]);
                        p0 = p;
                    }
                    _ => {}
                }
                element_counter += 1.0;
            }
            result.extend([-9999.0, element_counter]);
    }

    return result;
    }
}

