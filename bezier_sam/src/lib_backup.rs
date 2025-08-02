use std::ops::Range;
use std::ops::Sub;

use bezier_rs::Identifier;
use bezier_rs::ManipulatorGroup;
use bezier_rs::Subpath;
use bezier_rs::SubpathTValue;
use bezier_rs::TValueType;
// use glam::DVec2;
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

fn find_previous(sp: &Subpath<NoId>, spvec: &Vec<Subpath<NoId>>) -> usize {
    godot_print!("find previous");
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
        godot_print!("{differ}");
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
    fn point_along_cubic_parametric(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
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
            if trmmd.length(Some(3)) >= 1.0 && !is_subpath_in_list(&trmmd, &trimmed_sp1){
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
            if trmmd.length(Some(3)) >= 1.0 && !is_subpath_in_list(&trmmd, &trimmed_sp2){
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
