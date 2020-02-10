import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdministrationComponent } from './administration.component';
import { RouterModule } from '@angular/router';

@NgModule({
    declarations: [AdministrationComponent],
    imports: [
        CommonModule,
        RouterModule.forChild([{ path: '', component: AdministrationComponent }])
    ]
})
export class AdministrationModule { }
