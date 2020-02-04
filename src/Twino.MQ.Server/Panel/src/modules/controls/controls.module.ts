import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InputComponent } from './input/input.component';
import { SelectComponent } from './select/select.component';
import { CheckboxComponent } from './checkbox/checkbox.component';

@NgModule({
  declarations: [InputComponent, SelectComponent, CheckboxComponent],
  imports: [
    CommonModule
  ]
})
export class ControlsModule { }
